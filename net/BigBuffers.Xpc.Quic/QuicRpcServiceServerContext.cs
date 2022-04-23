using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public sealed class QuicRpcServiceServerContext : QuicRpcServiceContext, IDisposable {

  public QuicRpcServiceServerBase Server { get; private set; }

  public QuicRpcServiceServerContext(QuicRpcServiceServerBase server, QuicPeerConnection connection)
    : base(connection, server.Logger, true)
    => Server = server;

  protected override MessageStreamContext CreateMessageStreamContext(long msgId) {
    var newStream = base.CreateMessageStreamContext(msgId);
    Server.OnNewClientStream(this, msgId, newStream);
    return newStream;
  }

  protected override bool TryHandleControlMessage(IMessage msg) {
    if (base.TryHandleControlMessage(msg))
      return true;

    Logger?.WriteLine("Unhandled control message!");
    return true;
  }
  
  protected override void NewMessageStreamHandler(IMessage msg)
    => Server.Dispatch(msg);

  public void Dispose() {
    while (!MessageStreams.IsEmpty) {
      foreach (var streamKv in MessageStreams) {
#if NETSTANDARD2_0 || NETSTANDARD2_1
        if (!MessageStreams.TryRemove(streamKv.Key, out var _)) continue;
#else
        if (!MessageStreams.TryRemove(streamKv)) continue;
#endif
        var apc = streamKv.Value;
        apc.Dispose();
      }
    }

    Server = null!;
  }

}
