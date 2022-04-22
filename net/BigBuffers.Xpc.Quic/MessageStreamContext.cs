using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public sealed class MessageStreamContext : IDisposable {

  private QuicRpcServiceContext _ctx;

  public readonly AsyncProducerConsumerCollection<IMessage> Messages;

  private long _messageCounter;

  public MessageStreamContext(QuicRpcServiceContext ctx) {
    _ctx = ctx;
    Messages = new(new ConcurrentQueue<ReplyMessage>());
    _messageCounter = 0;
  }

  /// <summary>
  /// Count of messages processed.
  /// </summary>
  public long MessageCounter => _messageCounter;

  public bool IsAddingCompleted => Messages.IsAddingCompleted;

  public bool IsCompleted => Messages.IsCompleted;

  public bool TryAdd(IMessage message) {
    message.Context = _ctx;
    return Messages.TryAdd(message);
  }

  public void IncrementCounter() {
    Interlocked.Increment(ref _messageCounter);
  }

  public bool TryTakeMessage(out IMessage message) {
    if (!Messages.TryTake(out message))
      return false;

    IncrementCounter();
    return true;
  }

  public IAsyncConsumer<IMessage> GetConsumer() {
    var consumer = Messages.GetConsumer();
    return new MessageStreamContextConsumer(consumer);
  }

  public void Dispose() {
    Messages.Dispose();
  }

  public void Clear() {
    Messages.Clear();
  }

  public void CompleteAdding() {
    Messages.CompleteAdding();
  }

}

public class MessageStreamContextConsumer : IAsyncConsumer<IMessage> {

  private readonly AsyncProducerConsumerCollection<IMessage>.Consumer _consumer;

  public MessageStreamContextConsumer(AsyncProducerConsumerCollection<IMessage>.Consumer consumer)
    => _consumer = consumer;

  public IAsyncEnumerator<IMessage> GetAsyncEnumerator(CancellationToken cancellationToken = new())
    => _consumer.GetAsyncEnumerator(cancellationToken);

  public IEnumerator<IMessage> GetEnumerator()
    => _consumer.GetEnumerator();

  IEnumerator IEnumerable.GetEnumerator()
    => ((IEnumerable)_consumer).GetEnumerator();

  public async ValueTask DisposeAsync() {
    await _consumer.DisposeAsync();
  }

  public async ValueTask<bool> MoveNextAsync()
    => await _consumer.MoveNextAsync();

  public bool MoveNext()
    => _consumer.MoveNext();

  public void Reset()
    => _consumer.Reset();

  object IEnumerator.Current
    => Current;

  public IMessage Current
    => _consumer.Current;

  public bool TryMoveNext(out IMessage? item)
    => _consumer.TryMoveNext(out item);

  public async ValueTask WaitForAvailableAsync(bool continueOnCapturedContext, CancellationToken cancellationToken)
    => await _consumer.WaitForAvailableAsync(continueOnCapturedContext, cancellationToken);

  public bool IsEmpty
    => _consumer.IsEmpty;

  public bool IsCompleted
    => _consumer.IsCompleted;

  public void Dispose()
    => _consumer.Dispose();

}
