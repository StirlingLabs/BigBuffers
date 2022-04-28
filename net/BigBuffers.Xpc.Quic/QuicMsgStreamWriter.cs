using System;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using static BigBuffers.Xpc.Quic.QuicRpcServiceServerBase;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public class QuicMsgStreamWriter<T> : ChannelWriter<T>, IAsyncDisposable, IDisposable
  where T : struct, IBigBufferEntity
{
  private readonly TextWriter? _logger;
  protected QuicRpcServiceServerContext Context { get; }

  protected long MessageId { get; }

  private Task? _active;

  private bool _completedAdding;

  private bool _disposed;

  public QuicMsgStreamWriter(QuicRpcServiceServerContext context, long msgId, TextWriter? logger = null)
  {
    _logger = logger;
    Context = context;
    MessageId = msgId;
  }

  public override bool TryWrite(T item)
  {
    if (_disposed) return false;
    if (_completedAdding) return false;
    if (_active is not null)
    {
      if (_active.IsFaulted)
        throw _active.Exception!;
      if (_active.IsCanceled)
        throw new TaskCanceledException(_active);
      if (!_active.IsCompleted)
        return false;
    }
    _logger?.WriteLine(
      $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: sending {typeof(T).Name} stream message");

    _active = new ReplyMessage(Context, MessageType.Reply,
      new(item.Model.ByteBuffer.ToSizedMemory()),
      MessageId).SendAsync();
    return true;
  }

  public override async ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
  {
    if (_disposed) return false;

    if (_completedAdding) return false;

    if (_active is null)
      return true;

    await _active;

    if (_active.IsFaulted)
      throw _active.Exception!;

    if (_active.IsCanceled)
      throw new TaskCanceledException(_active);

    return true;
  }


  public override bool TryComplete(Exception? error = null)
  {
    _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: completing");
    _completedAdding = true;
    return true;
  }

  private async Task CloseAsync()
  {
    if (_active is not null)
      await _active;

    _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: trying to close");

    var final = new ReplyMessage(Context, MessageType.FinalControlReply, new(), MessageId);
#if NET5_0_OR_GREATER
    _logger?.WriteLine(
      $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: sending {typeof(T).Name} final control stream message: 0x{Convert.ToHexString((Span<byte>)final.Raw.BigSpan)}");
#endif
    await final.SendAsync();

    //ctx.Aio.Wait();
  }

  private void Close()
    => CloseAsync().GetAwaiter().GetResult();

  public async ValueTask DisposeAsync()
  {
    if (_disposed) return;
    _disposed = true;
    await CloseAsync();
    _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: disposing async");

    if (_active is null) return;

    await _active;
    _active.Dispose();
    _active = null;
  }

  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    Close();
    _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: disposing");

    if (_active is null) return;

    _active.ConfigureAwait(true).GetAwaiter().GetResult();
    _active.Dispose();
    _active = null;

  }
}
