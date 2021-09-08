#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Generated;
using nng;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Tests
{
  public class RpcService2NngServerImpl : RpcService2Nng.Server
  {
    public RpcService2NngServerImpl(IPairSocket ctx, IAPIFactory<INngMsg> factory, TextWriter? logger = null)
      : base(ctx, factory, logger) { }

    private readonly AsyncProducerConsumerCollection<Message> _messages = new(new ConcurrentQueue<Message>());

    public override Task<Status> Send(Message message, CancellationToken cancellationToken)
    {

      static Status Succeed()
      {
        var bb = new BigBufferBuilder();
        return new(Status.CreateStatus(bb).Value, bb.ByteBuffer);
      }

      static Status Fail()
      {
        var bb = new BigBufferBuilder();
        return new(Status.CreateStatus(bb, StatusCode.Failure).Value, bb.ByteBuffer);
      }

      if (!message.IsValid())
        return Task.FromResult(Fail());

      switch (message.BodyType)
      {
        case MessageBody.TextMessage:
          var str = message.BodyAsTextMessage().Content;
          switch (str)
          {
            case "Succeed": return Task.FromResult(Succeed());
            case "Fail": return Task.FromResult(Fail());
            default: {
              var added = _messages.TryAdd(message);
              Debug.Assert(added);
              return Task.FromResult(Succeed());
            }
          }

        case MessageBody.Empty: {
          var added = _messages.TryAdd(message);
          Debug.Assert(added);
          return Task.FromResult(Succeed());
        }

        case MessageBody.DataMessage:
          var data = message.BodyAsDataMessage().GetContentSpan();
          if (data.Length == 1)
          {
            var b = data[0u];
            return b switch
            {
              0 => Task.FromResult(Succeed()),
              1 => Task.FromResult(Fail()),
              _ => throw new NotImplementedException()
            };
          }
          else
          {
            var added = _messages.TryAdd(message);
            Debug.Assert(added);
            return Task.FromResult(Succeed());
          }

        default:
          throw new InvalidOperationException();
      }
    }

    public override async Task<Status> SendManyAtomic(ChannelReader<Message> messages, CancellationToken cancellationToken)
    {
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendManyAtomic getting messages");
      var bb = new BigBufferBuilder();
      var status = Status.CreateStatus(bb).Resolve(bb);
      await foreach (var message in messages.AsConsumingAsyncEnumerable(cancellationToken))
      {
        _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendManyAtomic got a message");
        status = await Send(message, cancellationToken);
        if (status.Code == StatusCode.Failure)
          break;
      }
      //Debug.Assert(messages.Completion.IsCompleted);
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendManyAtomic completed (status: {status.Code})");
      return status;
    }

    public override async Task SendMany(ChannelReader<Message> messages, ChannelWriter<Status> statuses, CancellationToken cancellationToken)
    {
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendMany getting messages");
      await foreach (var message in messages.AsConsumingAsyncEnumerable(cancellationToken))
      {
        _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendMany got a message");
        await statuses.WriteAsync(await Send(message, cancellationToken), cancellationToken);
        _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendMany wrote a status");
      }
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendMany done getting messages (completed: {messages.Completion.IsCompleted})");
      Debug.Assert(messages.Completion.IsCompleted || cancellationToken.IsCancellationRequested);
      statuses.TryComplete();
      cancellationToken.ThrowIfCancellationRequested();
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: SendMany completed");
    }

    public override async Task ReceiveMany(Empty empty, ChannelWriter<Message> messages, CancellationToken cancellationToken)
    {
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: ReceiveMany started");
      foreach (var message in _messages.GetConsumer())
      {
        _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: ReceiveMany sending a message");
        await messages.WriteAsync(message, cancellationToken);
        _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: ReceiveMany sent a message");
      }
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: ReceiveMany completing");
      var completed = messages.TryComplete();
      Debug.Assert(completed);
      _logger?.WriteLine($"[{TimeStamp:F3}] T{Task.CurrentId}: ReceiveMany completed");
    }

    public override async Task<Message> Receive(Empty _, CancellationToken cancellationToken)
    {
      await foreach (var message in _messages.GetConsumer().WithCancellation(cancellationToken))
        return message;

      var bb = new BigBufferBuilder();
      Message.StartMessage(bb);
      Message.AddBody(bb, bb.MarkOffsetPlaceholder<Empty>(out var body).Value);
      Message.AddBodyType(bb, MessageBody.TextMessage);
      var msg = Message.EndMessage(bb).Resolve(bb);
      Empty.StartEmpty(bb);
      var empty = Empty.EndEmpty(bb);
      body.Fill(empty);

      return msg;
    }
  }
}
