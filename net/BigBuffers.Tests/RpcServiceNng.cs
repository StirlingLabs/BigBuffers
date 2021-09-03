#nullable enable
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BigBuffers.Xpc;
using BigBuffers.Xpc.Nng;
using Generated;
using nng;

namespace BigBuffers.Tests
{
  public static class RpcServiceNng
  {
    private static readonly Type ServiceInterfaceType = typeof(IRpcService);
    private static readonly byte[] Utf8ServiceId = Encoding.UTF8.GetBytes(ServiceInterfaceType.Namespace + ".RpcService");
    private static readonly byte[] Utf8CallSend = Encoding.UTF8.GetBytes("Send");
    private static readonly byte[] Utf8CallReceive = Encoding.UTF8.GetBytes("Receive");

    private enum Method
    {
      Send = 1,
      Receive
    }


    private static ReadOnlySpan<byte> StaticResolveMethodSignature(Method method)
      => method switch
      {
        Method.Send => Utf8CallSend,
        Method.Receive => Utf8CallReceive,
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };

    public class Client : NngRpcServiceClientBase, IRpcService
    {
      public Client(IPairSocket pair, IAPIFactory<INngMsg> factory, TextWriter logger)
        : base(pair, factory, logger) { }

      public override ReadOnlySpan<byte> ServiceId => Utf8ServiceId;

      protected override ReadOnlySpan<byte> ResolveMethodSignature<TMethodEnum>(TMethodEnum method)
        => method is Method m ? StaticResolveMethodSignature(m) : throw new InvalidOperationException();

      public async Task<Status> Send(Message message, CancellationToken cancellationToken)
        => await UnaryRequest<Method, Status, Message>(Method.Send, message, cancellationToken);

      public async Task<Message> Receive(Empty empty, CancellationToken cancellationToken)
        => await UnaryRequest<Method, Message, Empty>(Method.Receive, empty, cancellationToken);
    }


    public abstract class Server : NngRpcServiceServerBase, IRpcService
    {
      protected Server(IPairSocket pair, IAPIFactory<INngMsg> factory, TextWriter? logger = null)
        : base(pair, factory, logger) { }

      public override ReadOnlySpan<byte> ServiceId => Utf8ServiceId;

      protected override ReadOnlySpan<byte> ResolveMethodSignature<TMethodEnum>(TMethodEnum method)
        => method is Method m ? StaticResolveMethodSignature(m) : throw new InvalidOperationException();

      protected override async Task<INngMsg?> DispatchUnary<TMethodEnum>(TMethodEnum method, long msgId, ByteBuffer sourceByteBuffer,
        CancellationToken cancellationToken)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        return m switch
        {
          Method.Send => Factory.CreateReply(msgId, await Send(new(0, sourceByteBuffer), cancellationToken)),
          Method.Receive => Factory.CreateReply(msgId, await Receive(new(0, sourceByteBuffer), cancellationToken)),
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }
      
      // No Client Streaming
      
      // No Server Streaming

      // No Bidirectional Streaming

      protected override RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        return m switch
        {
          Method.Send => RpcMethodType.Unary,
          Method.Receive => RpcMethodType.Unary,
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      public override async Task RunAsync(CancellationToken cancellationToken)
        => await base.RunAsync<Method>(cancellationToken);

      public abstract Task<Status> Send(Message message, CancellationToken cancellationToken);

      public abstract Task<Message> Receive(Empty empty, CancellationToken cancellationToken);
    }
  }
}
