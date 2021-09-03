using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BigBuffers.Xpc
{
  public static class EnumerableChannelReader
  {
    public static EnumerableChannelReader<T> Create<T>(IEnumerable<T> items)
      => new(items);
  }

  public class EnumerableChannelReader<T> : ChannelReader<T>
  {
    private readonly IEnumerator<T> _items;
#if NETSTANDARD
    private readonly TaskCompletionSource<bool> _tcs = new();
#else
    private readonly TaskCompletionSource _tcs = new();
#endif
    public EnumerableChannelReader(IEnumerable<T> items)
      => _items = items.GetEnumerator();

    public override bool TryRead(out T item)
    {
      Unsafe.SkipInit(out item);
      if (Completion.IsCompleted)
        return false;

      if (_items.MoveNext())
      {
        item = _items.Current;
        return true;
      }
#if NETSTANDARD
      _tcs.TrySetResult(true);
#else
      _tcs.TrySetResult();
#endif
      return false;
    }

    public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
      => new(!Completion.IsCompleted);

    public override Task Completion => _tcs.Task;
  }
}
