using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using ZeroMQ;

namespace BigBuffers.Xpc.ZeroMq
{
  [PublicAPI]
  public class ZeroMqMessageZFrameWriter<T> : ChannelWriter<T>, IAsyncDisposable, IDisposable
    where T : struct, IBigBufferEntity
  {
    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    private readonly TextWriter? _logger;

    protected long MessageId { get; }

    public uint RoutingId { get; }

    protected readonly ZSocket Socket;

    private object _activeLock = new();
    private Task? _active = Task.CompletedTask;

    private bool _completedAdding;

    private bool _disposed;

    public ZeroMqMessageZFrameWriter(ZSocket socket, uint routingId, long messageId, TextWriter? logger = null)
    {
      _logger = logger;
      Socket = socket;
      RoutingId = routingId;
      MessageId = messageId;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: created");
    }

    public override async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
      do
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryWrite(item))
          return;
        cancellationToken.ThrowIfCancellationRequested();
      } while (await WaitToWriteAsync(cancellationToken));

      throw new NotImplementedException();
    }

    public override bool TryWrite(T item)
    {
      var socket = Socket;

      if (_disposed)
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: not able to write new message, disposed");
        return false;
      }
      if (_completedAdding)
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: not able to write new message, already completed adding");
        return false;
      }

      if (_active is not null)
      {
        if (_active.IsFaulted)
          throw _active.Exception!;
        if (_active.IsCanceled)
          throw new TaskCanceledException(_active);
        if (!_active.IsCompleted)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: not able to write new message, currently writing");
          return false;
        }
      }

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: sending {typeof(T).Name} stream message (1)");

      var inputMem = item.Model.ByteBuffer.ToSizedReadOnlyMemory();
      lock (_activeLock)
        _active = Task.Run(async () => {

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: sending {typeof(T).Name} stream message");

          try
          {
            using var frame = ZFrame.Create(inputMem);
            frame.TrySetRoutingId(RoutingId);
            Socket.SendFrame(frame);
          }
          finally
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: cleaning up after sending {typeof(T).Name} stream message");

            lock (_activeLock)
              _active = Task.CompletedTask;

            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: cleaned up after sending {typeof(T).Name} stream message");
          }
        });

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
      var socket = Socket;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: completing");
      _completedAdding = true;
      return true;
    }

    private async Task CloseAsync()
    {
      var socket = Socket;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: closing");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: awaiting active write completion");

      await _active;

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: active write completed");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: finished");
    }

    public async ValueTask DisposeAsync()
    {
      if (_disposed) return;
      _disposed = true;
      var socket = Socket;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: disposing async");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: closing from dispose");
      await CloseAsync();

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{socket.Name}: disposed async");
    }

    public void Dispose()
      => DisposeAsync().GetAwaiter().GetResult();
  }
}
