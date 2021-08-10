using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
{
  public delegate bool TryAddFunc<T>(in T item);

  public delegate bool TryTakeFunc<T>(out T item);

  [PublicAPI]
  public static class DeferredProducerConsumerCollection
  {
    public static IProducerConsumerCollection<T> Create<T>(TryAddFunc<T> tryAdd, TryTakeFunc<T> tryTake, bool synchronized)
      => synchronized
        ? new DeferredSynchronizedProducerConsumerCollection<T>(tryAdd, tryTake)
        : new DeferredProducerConsumerCollection<T>(tryAdd, tryTake);
  }

  public class DeferredSynchronizedProducerConsumerCollection<T> : IProducerConsumerCollection<T>
  {
    private readonly TryAddFunc<T> _tryAdd;
    private readonly TryTakeFunc<T> _tryTake;
    internal DeferredSynchronizedProducerConsumerCollection(TryAddFunc<T> tryAdd, TryTakeFunc<T> tryTake)
    {
      _tryAdd = tryAdd;
      _tryTake = tryTake;
    }

    public IEnumerator<T> GetEnumerator()
      => throw new NotSupportedException();

    IEnumerator IEnumerable.GetEnumerator()
      => GetEnumerator();

    public void CopyTo(Array array, int index)
      => throw new NotSupportedException();

    public int Count => throw new NotSupportedException();

    public bool IsSynchronized => true;

    public object SyncRoot { get; } = new();

    public void CopyTo(T[] array, int index)
      => throw new NotSupportedException();

    public T[] ToArray()
      => throw new NotSupportedException();

    public bool TryAdd(T item)
    {
      lock (SyncRoot)
        return _tryAdd(item);
    }

    public bool TryTake(out T item)
    {
      lock (SyncRoot)
        return _tryTake(out item);
    }
  }


  public class DeferredProducerConsumerCollection<T> : IProducerConsumerCollection<T>
  {
    private readonly TryAddFunc<T> _tryAdd;
    private readonly TryTakeFunc<T> _tryTake;
    internal DeferredProducerConsumerCollection(TryAddFunc<T> tryAdd, TryTakeFunc<T> tryTake)
    {
      _tryAdd = tryAdd;
      _tryTake = tryTake;
    }

    public IEnumerator<T> GetEnumerator()
      => throw new NotSupportedException();

    IEnumerator IEnumerable.GetEnumerator()
      => GetEnumerator();

    public void CopyTo(Array array, int index)
      => throw new NotSupportedException();

    public int Count => throw new NotSupportedException();

    public bool IsSynchronized => false;

    public object SyncRoot
      => throw new NotSupportedException();

    public void CopyTo(T[] array, int index)
      => throw new NotSupportedException();

    public T[] ToArray()
      => throw new NotSupportedException();

    public bool TryAdd(T item)
      => _tryAdd(item);

    public bool TryTake(out T item)
      => _tryTake(out item);
  }
}
