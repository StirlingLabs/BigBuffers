#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Generated;
using nng;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Tests
{
  public class RpcServiceNngServerImpl : RpcServiceNng.Server
  {
    public RpcServiceNngServerImpl(IPairSocket ctx, IAPIFactory<INngMsg> factory, TextWriter? logger = null)
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
            switch (b)
            {
              case 0:
                return Task.FromResult(Succeed());

              case 1:
                return Task.FromResult(Fail());

              default:
                throw new NotImplementedException();
            }
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

    public override async Task<Message> Receive(Empty _, CancellationToken cancellationToken)
    {
      await foreach (var message in _messages.GetConsumer().WithCancellation(cancellationToken))
        return message;

      throw new ObjectDisposedException(nameof(RpcServiceNngServerImpl));
    }
  }
}
