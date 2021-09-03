#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BigBuffers.Xpc;
using BigBuffers.Xpc.Nng;
using Generated;
using nng;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Tests
{
  public static class RpcService2Nng
  {
    private static readonly Type ServiceInterfaceType = typeof(IRpcService2);
    private static readonly byte[]
      Utf8ServiceId = Encoding.UTF8.GetBytes(ServiceInterfaceType.Namespace + ".RpcService"),
      Utf8CallSend = Encoding.UTF8.GetBytes("Send"),
      Utf8CallReceive = Encoding.UTF8.GetBytes("Receive"),
      Utf8CallSendManyAtomic = Encoding.UTF8.GetBytes("SendManyAtomic"),
      Utf8CallSendMany = Encoding.UTF8.GetBytes("SendMany"),
      Utf8CallReceiveMany = Encoding.UTF8.GetBytes("ReceiveMany");

    private enum Method
    {
      Send = 1,
      Receive,
      SendManyAtomic,
      SendMany,
      ReceiveMany
    }

    private static ReadOnlySpan<byte> StaticResolveMethodSignature(Method method)
      => method switch
      {
        Method.Send => Utf8CallSend,
        Method.Receive => Utf8CallReceive,
        Method.SendManyAtomic => Utf8CallSendManyAtomic,
        Method.SendMany => Utf8CallSendMany,
        Method.ReceiveMany => Utf8CallReceiveMany,
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };

    public class Client : NngRpcServiceClientBase, IRpcService2
    {
      public Client(IPairSocket pair, IAPIFactory<INngMsg> factory, TextWriter logger)
        : base(pair, factory, logger) { }

      public override ReadOnlySpan<byte> ServiceId => Utf8ServiceId;

      protected override ReadOnlySpan<byte> ResolveMethodSignature<TMethodEnum>(TMethodEnum method)
        => method is Method m ? StaticResolveMethodSignature(m) : throw new InvalidOperationException();

      // implementation starts here

      public async Task<Status> Send(Message message, CancellationToken cancellationToken)
        => await UnaryRequest<Method, Status, Message>(Method.Send, message, cancellationToken);

      public async Task<Message> Receive(Empty empty, CancellationToken cancellationToken)
        => await UnaryRequest<Method, Message, Empty>(Method.Receive, empty, cancellationToken);

      public async Task<Status> SendManyAtomic(ChannelReader<Message> message, CancellationToken cancellationToken)
        => await ClientStreamingRequest<Method, Status, Message>(Method.SendManyAtomic,
          message.AsConsumingAsyncEnumerable(cancellationToken),
          cancellationToken);

      public Task SendMany(ChannelReader<Message> message, ChannelWriter<Status> status, CancellationToken cancellationToken)
        => StreamingRequest<Method, Status, Message>(Method.SendMany,
          message.AsConsumingAsyncEnumerable(cancellationToken),
          cancellationToken).WriteTo(status, cancellationToken);

      public Task ReceiveMany(Empty empty, ChannelWriter<Message> message, CancellationToken cancellationToken)
        => ServerStreamingRequest<Method, Message, Empty>(Method.ReceiveMany,
          empty,
          cancellationToken).WriteTo(message, cancellationToken);
    }


    public abstract class Server : NngRpcServiceServerBase, IRpcService2
    {
      protected Server(IPairSocket pair, IAPIFactory<INngMsg> factory, TextWriter? logger = null)
        : base(pair, factory, logger) { }

      public override ReadOnlySpan<byte> ServiceId => Utf8ServiceId;

      protected override ReadOnlySpan<byte> ResolveMethodSignature<TMethodEnum>(TMethodEnum method)
        => method is Method m ? StaticResolveMethodSignature(m) : throw new InvalidOperationException();

      protected override RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        return m switch
        {
          Method.Send => RpcMethodType.Unary,
          Method.Receive => RpcMethodType.Unary,
          Method.SendManyAtomic => RpcMethodType.ClientStreaming,
          Method.SendMany => RpcMethodType.BidirectionalStreaming,
          Method.ReceiveMany => RpcMethodType.ServerStreaming,
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override async Task<INngMsg?> DispatchUnary<TMethodEnum>(TMethodEnum method, long msgId, ByteBuffer sourceByteBuffer,
        CancellationToken cancellationToken)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        async Task<INngMsg?> Reply<T>(Task<T> task)
          where T : struct, IBigBufferEntity
          => Factory.CreateReply(msgId, await task);

        return m switch
        {
          Method.Send => await Reply(Send(new(0, sourceByteBuffer), cancellationToken)),
          Method.Receive => await Reply(Receive(new(0, sourceByteBuffer), cancellationToken)),
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override async Task<INngMsg?> DispatchClientStreaming<TMethodEnum>(TMethodEnum method, long msgId,
        AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> reader, CancellationToken cancellationToken)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        async Task<INngMsg?> Reply<T>(Task<T> task)
          where T : struct, IBigBufferEntity
          => Factory.CreateReply(msgId, await task);

        return m switch
        {
          Method.SendManyAtomic => await Reply(SendManyAtomic(WrapReader<Message>(reader), cancellationToken)),
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override Task DispatchServerStreaming<TMethodEnum>(TMethodEnum method, long msgId, ByteBuffer sourceByteBuffer,
        CancellationToken cancellationToken)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        var ads = new List<IAsyncDisposable>(1);

        async Task CleanUpContinuation(Task _)
        {
          foreach (var ad in ads)
            await ad.DisposeAsync();
        }

        Task CleanUpAfter(Task t) // ReSharper disable once MethodSupportsCancellation
          => t.ContinueWith(CleanUpContinuation);

        ChannelWriter<T> Writer<T>()
          where T : struct, IBigBufferEntity
          => Track(new NngMsgStreamWriter<T>(this, msgId, _logger), ads);

        return m switch
        {
          Method.ReceiveMany => CleanUpAfter(ReceiveMany(new(0, sourceByteBuffer), Writer<Message>(), cancellationToken)),
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override Task DispatchStreaming<TMethodEnum>(TMethodEnum method, long msgId,
        AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> reader, CancellationToken cancellationToken)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        var ads = new List<IAsyncDisposable>(1);

        async Task CleanUpContinuation(Task _)
        {
          foreach (var ad in ads)
            await ad.DisposeAsync();
        }

        Task CleanUpAfter(Task t) // ReSharper disable once MethodSupportsCancellation
          => t.ContinueWith(CleanUpContinuation);

        ChannelWriter<T> Writer<T>()
          where T : struct, IBigBufferEntity
          => Track(new NngMsgStreamWriter<T>(this, msgId, _logger), ads);

        return m switch
        {
          Method.SendMany => CleanUpAfter(SendMany(WrapReader<Message>(reader), Writer<Status>(), cancellationToken)),
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      public override async Task RunAsync(CancellationToken cancellationToken)
        => await base.RunAsync<Method>(cancellationToken);
      
      // implementation starts here

      public abstract Task<Status> Send(Message message, CancellationToken cancellationToken);

      public abstract Task<Message> Receive(Empty empty, CancellationToken cancellationToken);

      public abstract Task<Status> SendManyAtomic(ChannelReader<Message> message, CancellationToken cancellationToken);

      public abstract Task SendMany(ChannelReader<Message> message, ChannelWriter<Status> status, CancellationToken cancellationToken);

      public abstract Task ReceiveMany(Empty empty, ChannelWriter<Message> message, CancellationToken cancellationToken);
    }
  }
}
