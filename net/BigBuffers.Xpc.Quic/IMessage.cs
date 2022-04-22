using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public interface IMessage
{
  long Id { get; set; }

  MessageType Type { get; set; }

  BigMemory<byte> Raw { get; }

  Task SendAsync();

  bool StreamOnly { get; set; }

  uint HeaderSize { get; set; }

  BigSpan<byte> Header { get; }

  BigSpan<byte> Body { get; }

  ref byte GetPinnableReference();

  IQuicRpcServiceContext? Context { get; set; }
}
