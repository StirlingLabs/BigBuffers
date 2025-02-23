using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;
public class EntityQuicMsgChannelReader<T> : ChannelReader<T> where T : struct, IBigBufferEntity
{
  private readonly TextWriter? _logger;
  private readonly AsyncProducerConsumerCollection<IMessage> _collection;

  public EntityQuicMsgChannelReader(AsyncProducerConsumerCollection<IMessage> collection, TextWriter? logger = null)
  {
    _collection = collection;
    _logger = logger;
  }


#if NETSTANDARD
    private readonly TaskCompletionSource<bool> _tcs = new();
#else
  private readonly TaskCompletionSource _tcs = new();
#endif

  public override bool TryRead(out T item)
  {
    Unsafe.SkipInit(out item);
    if (Completion.IsCompleted)
    {
      _logger?.WriteLine(
        $"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, messages previously completed");
      return false;
    }

    if (!_collection.TryTake(out var msg))
    {

      // ReSharper disable once InvertIf
      if (_collection.IsCompleted)
      {
        _logger?.WriteLine(
          $"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, messages completed");
#if NETSTANDARD
          _tcs.SetResult(true);
#else
        _tcs.SetResult();
#endif
        return false;
      }

      _logger?.WriteLine(
        $"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, no messages yet");
      return false;
    }

    item = new() { Model = new(msg.Body.Length > 0 ? new(msg.Body) : new(0), 0) };
    _logger?.WriteLine($"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{msg.Id} T{Task.CurrentId}: read entity");
    return true;
  }

  public override async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
  {
    if (Completion.IsCompleted)
    {
      _logger?.WriteLine(
        $"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to wait, previously completed");
      return false;
    }

    if (!_collection.IsCompleted)
    {
      _logger?.WriteLine($"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting to read");
      try {
        if (!await _collection.TryWaitForAvailableAsync(false, cancellationToken))
          return false;
      }
      catch (OperationCanceledException) {
        if (_collection.IsCompleted)
          return false;

        throw;
      }

      if (!_collection.IsCompleted)
      {
        _logger?.WriteLine($"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting completed");
        return true;
      }

      _logger?.WriteLine($"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting failed");
    }

    _logger?.WriteLine($"[{QuicRpcServiceServerBase.TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: collection was completed");

#if NETSTANDARD
      _tcs.SetResult(true);
#else
    _tcs.SetResult();
#endif

    return false;
  }

  public override Task Completion => _tcs.Task;
}
