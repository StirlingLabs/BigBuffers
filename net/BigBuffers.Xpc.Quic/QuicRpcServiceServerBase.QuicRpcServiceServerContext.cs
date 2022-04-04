using System;
using System.Collections.Concurrent;
using JetBrains.Annotations;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;

public abstract partial class QuicRpcServiceServerBase
{
  [PublicAPI]
  public sealed class QuicRpcServiceServerContext : QuicRpcServiceContext, IDisposable
  {
    public QuicRpcServiceServerBase Server { get; private set; }

    internal readonly ConcurrentDictionary<long, AsyncProducerConsumerCollection<IMessage>>
      ClientMsgStreams = new();

    public QuicRpcServiceServerContext(QuicRpcServiceServerBase server, QuicPeerConnection connection)
      : base(connection, server.Logger, true)
      => Server = server;


    public void Dispose()
    {
      while (!ClientMsgStreams.IsEmpty)
      {
        foreach (var streamKv in ClientMsgStreams)
        {
#if NETSTANDARD2_0 || NETSTANDARD2_1
          if (!ClientMsgStreams.TryRemove(streamKv.Key, out var _)) continue;
#else
          if (!ClientMsgStreams.TryRemove(streamKv)) continue;
#endif
          var apc = streamKv.Value;
          apc.Dispose();
        }
      }
      Server = null!;
    }
  }
}
