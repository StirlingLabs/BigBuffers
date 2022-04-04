using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Http
{
  public class EntityHttpMsgChannelReader<T> : ChannelReader<T> where T : struct, IBigBufferEntity
  {
    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    private readonly AsyncProducerConsumerCollection<ByteBuffer> _collection;
    private readonly TextWriter? _logger;

    public EntityHttpMsgChannelReader(AsyncProducerConsumerCollection<ByteBuffer> collection, TextWriter? logger = null)
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
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, messages previously completed");
        return false;
      }

      if (!_collection.TryTake(out var msg))
      {

        // ReSharper disable once InvertIf
        if (_collection.IsCompleted)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, messages completed");
#if NETSTANDARD
          _tcs.SetResult(true);
#else
          _tcs.SetResult();
#endif
          return false;
        }

        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, no messages yet");
        return false;
      }

      item = new() { Model = new(msg, 0) };
      _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: read entity");
      return true;
    }

    public override async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
      var total = Stopwatch.StartNew(); 
      for (;;)
      {
        var current = Stopwatch.StartNew();

        string TimeSpent()
          => $"{total.ElapsedTicks:0.0e0}t/{current.ElapsedTicks:0.0e0}t";

        if (Completion.IsCompleted)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to wait ({TimeSpent()}), previously completed");
          return false;
        }

        if (!_collection.IsCompleted)
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) to read");

          try
          {
            await _collection.WaitForAvailableAsync(false, cancellationToken);
          }
          catch (InvalidOperationException)
          {
            if (!_collection.IsCompleted)
              throw;
            
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: collection was completed (waited {TimeSpent()})");
#if NETSTANDARD
            _tcs.SetResult(true);
#else
            _tcs.SetResult();
#endif
            return false;
          }
          catch (OperationCanceledException)
          {
            if (cancellationToken.IsCancellationRequested)
            {
              _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) was cancelled");
              return false;
            }
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) was cancelled externally");
          }

          if (!_collection.IsCompleted)
          {
            if (_collection.IsEmpty && !cancellationToken.IsCancellationRequested)
            {
              if (current.ElapsedTicks >= 2000)
                _logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) was confused");
              else
              {
                _logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) is confused, yielding");
                var yielded = Stopwatch.StartNew();
                await Task.Yield();
                _logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: retrying wait (yielded {yielded.ElapsedTicks:0.0e0}t, total {TimeSpent()})");
              }
              continue;
            }
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) completed");
            return true;
          }

          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting ({TimeSpent()}) failed");
        }

        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: collection was completed (waited {TimeSpent()})");

#if NETSTANDARD
        _tcs.SetResult(true);
#else
        _tcs.SetResult();
#endif

        return false;
      }
    }

    public override Task Completion => _tcs.Task;
  }
}
