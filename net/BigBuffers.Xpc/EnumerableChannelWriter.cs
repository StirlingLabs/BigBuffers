#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BigBuffers.Xpc
{
  public class EnumerableChannelWriter<T> : ChannelWriter<T>, IDisposable
  {
    private ConcurrentQueue<T>? _queue = new();
    private bool _addingComplete;

    public bool AddingComplete
    {
      get => Volatile.Read(ref _addingComplete);
      private set => Volatile.Write(ref _addingComplete, value);
    }

    public override bool TryComplete(Exception? error = null)
    {
      AddingComplete = true;
      return true;
    }

    public override bool TryWrite(T item)
    {
      if (_queue is null)
        throw new ObjectDisposedException(nameof(EnumerableChannelWriter<T>));
      if (AddingComplete)
        return false;
      _queue.Enqueue(item);
      return true;
    }

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
      if (_queue is null) return new(false);
      return new(true);
    }

    public IEnumerable<T> AsEnumerable()
      => _queue ?? throw new ObjectDisposedException(nameof(EnumerableChannelWriter<T>));

    public void Dispose()
    {
      AddingComplete = true;
      _queue = null;
    }
  }
}
