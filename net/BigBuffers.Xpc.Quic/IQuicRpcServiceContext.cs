using StirlingLabs.MsQuic;

namespace BigBuffers.Xpc.Quic;

public interface IQuicRpcServiceContext
{
  QuicPeerConnection Connection { get; }
  QuicStream Stream { get; }
}
