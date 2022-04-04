using System;
using System.Collections.Concurrent;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;

public abstract partial class QuicRpcServiceServerBase
{
  public sealed class QuicRpcServiceServerContext : QuicRpcServiceContext
  {
    public QuicRpcServiceServerBase Server { get; }

    internal readonly ConcurrentDictionary<long, AsyncProducerConsumerCollection<IMessage>>
      ClientMsgStreams = new();

    public QuicRpcServiceServerContext(QuicRpcServiceServerBase server, QuicPeerConnection connection)
      : base(connection, server.Logger, true)
      => Server = server;
    
    
  }
}
