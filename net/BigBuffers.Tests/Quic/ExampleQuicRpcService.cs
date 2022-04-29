#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BigBuffers.Xpc;
using BigBuffers.Xpc.Quic;
using Generated;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Tests;

public static partial class ExampleQuicRpcService {

  private enum Method : long {

    Unary = 1,

    ClientStreaming = 2,

    ServerStreaming = 3,

    BidirectionalStreaming = 4

  }

  private static readonly SizedUtf8String SignatureUnary = "Unary";

  private static readonly SizedUtf8String SignatureClientStreaming = "ClientStreaming";

  private static readonly SizedUtf8String SignatureServerStreaming = "ServerStreaming";

  private static readonly SizedUtf8String SignatureBidirectionalStreaming = "BidirectionalStreaming";

  private static SizedUtf8String StaticResolveMethodSignature(Method method)
    => method switch {
      Method.Unary => SignatureUnary,
      Method.ClientStreaming => SignatureClientStreaming,
      Method.ServerStreaming => SignatureServerStreaming,
      Method.BidirectionalStreaming => SignatureBidirectionalStreaming,
      _ => throw new ArgumentOutOfRangeException(nameof(method))
    };

  public sealed partial class Client : QuicRpcServiceClientBase, IExampleQuicService {

    public Client(SizedUtf8String name, QuicPeerConnection connection, TextWriter? logger = null)
      : base(name, connection, logger) {
    }

    protected override SizedUtf8String ResolveMethodSignature<TMethodEnum>(TMethodEnum method) {
      if (method is not Method m)
        throw new InvalidOperationException();

      return StaticResolveMethodSignature(m);
    }

    public async Task<Message> Unary(Message message, CancellationToken ct = default)
      => await UnaryRequest<Method, Message, Message>(Method.Unary, message, ct);

    public async Task<Message> ClientStreaming(ChannelReader<Message> messages, CancellationToken ct = default)
      => await ClientStreamingRequest<Method, Message, Message>(Method.ClientStreaming, messages, ct);

    public async Task ServerStreaming(Message m, ChannelWriter<Message> writer, CancellationToken ct = default)
      => await ServerStreamingRequest<Method, Message, Message>(Method.ServerStreaming, m, ct).WriteTo(writer, ct);

    public async Task BidirectionalStreaming(ChannelReader<Message> messages, ChannelWriter<Message> writer, CancellationToken ct = default)
      => await StreamingRequest<Method, Message, Message>(Method.BidirectionalStreaming, messages, ct).WriteTo(writer, ct);

  }

  public sealed partial class Server : QuicRpcServiceServerBase, IExampleQuicService {

    private readonly IExampleQuicService _implementation;

    public Server(IExampleQuicService implementation, string name, QuicListener listener, TextWriter? logger = null)
      : base(name, listener, logger)
      => _implementation = implementation;

    protected override SizedUtf8String ResolveMethodSignature<TMethodEnum>(TMethodEnum method) {
      if (method is not Method m)
        throw new InvalidOperationException();

      return StaticResolveMethodSignature(m);
    }

    protected override RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method) {
      if (method is not Method m)
        throw new InvalidOperationException();

      return m switch {
        Method.Unary => RpcMethodType.Unary,
        Method.ClientStreaming => RpcMethodType.ClientStreaming,
        Method.ServerStreaming => RpcMethodType.ServerStreaming,
        Method.BidirectionalStreaming => RpcMethodType.BidirectionalStreaming,
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };
    }

    public override async Task Dispatch(IMessage sourceMsg, CancellationToken ct = default)
      => await Dispatch<Method>(sourceMsg, ct);

    protected override async Task<IMessage?> DispatchUnary<TMethodEnum>(
      TMethodEnum method,
      long sourceMsgId,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) {
      if (method is not Method m) throw new InvalidOperationException();

      return m switch {
        Method.Unary => await Reply(Unary(new(0, sourceByteBuffer), cancellationToken)),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };
    }

    protected override async Task<IMessage?> DispatchClientStreaming<TMethodEnum>(
      TMethodEnum method,
      long sourceMsgId,
      AsyncProducerConsumerCollection<IMessage> reader,
      CancellationToken cancellationToken
    ) {
      if (method is not Method m) throw new InvalidOperationException();

      return m switch {
        Method.ClientStreaming => await Reply(ClientStreaming(WrapReader<@Message>(reader), cancellationToken)),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };
    }

    protected override Task DispatchServerStreaming<TMethodEnum>(TMethodEnum method,
      long sourceMsgId,
      QuicRpcServiceServerContext ctx,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken) {
      if (method is not Method m) throw new InvalidOperationException();

      var ads = new List<IAsyncDisposable>(1);

      async Task CleanUpContinuation(Task _) {
        foreach (var ad in ads) await ad.DisposeAsync();
      }

      Task CleanUpAfter(Task t) => t.ContinueWith(CleanUpContinuation, cancellationToken: default);

      ChannelWriter<T> Writer<T>() where T : struct, IBigBufferEntity
        => Track(new QuicMsgStreamWriter<T>(ctx, sourceMsgId, Logger), ads);

      return m switch {
        Method.ServerStreaming => CleanUpAfter(ServerStreaming(new(0, sourceByteBuffer), Writer<@Message>(), cancellationToken)),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };
    }

    protected override Task DispatchStreaming<TMethodEnum>(TMethodEnum method,
      long sourceMsgId,
      QuicRpcServiceServerContext ctx,
      AsyncProducerConsumerCollection<IMessage> reader,
      CancellationToken cancellationToken) {
      if (method is not Method m) throw new InvalidOperationException();

      var ads = new List<IAsyncDisposable>(1);

      async Task CleanUpContinuation(Task _) {
        foreach (var ad in ads) await ad.DisposeAsync();
      }

      Task CleanUpAfter(Task t) => t.ContinueWith(CleanUpContinuation, cancellationToken: default);

      ChannelWriter<T> Writer<T>() where T : struct, IBigBufferEntity
        => Track(new QuicMsgStreamWriter<T>(ctx, sourceMsgId, Logger), ads);

      return m switch {
        Method.BidirectionalStreaming => CleanUpAfter(BidirectionalStreaming(WrapReader<@Message>(reader), Writer<@Message>(), cancellationToken)),
        _ => throw new ArgumentOutOfRangeException(nameof(method))
      };
    }

    public async Task<Message> Unary(Message m, CancellationToken ct = default)
      => await _implementation.Unary(m, ct);

    public async Task<Message> ClientStreaming(ChannelReader<Message> msgs, CancellationToken ct = default)
      => await _implementation.ClientStreaming(msgs, ct);

    public async Task ServerStreaming(Message m, ChannelWriter<Message> writer, CancellationToken ct = default)
      => await _implementation.ServerStreaming(m, writer, ct);

    public async Task BidirectionalStreaming(ChannelReader<Message> msgs, ChannelWriter<Message> writer, CancellationToken ct = default)
      => await _implementation.BidirectionalStreaming(msgs, writer, ct);

  }

}
