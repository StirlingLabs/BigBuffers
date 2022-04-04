using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public sealed class DoNotSendMessage : IMessage
{
  public long Id => throw new NotSupportedException();

  public MessageType Type => throw new NotSupportedException();

  public BigMemory<byte> Raw => throw new NotSupportedException();

  public Task SendAsync() => throw new NotSupportedException();

  public bool StreamOnly
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  public uint HeaderSize
  {
    get => throw new NotSupportedException();
    set => throw new NotSupportedException();
  }

  public BigSpan<byte> Header => throw new NotSupportedException();

  public BigSpan<byte> Body => throw new NotSupportedException();

  public ref byte GetPinnableReference() => throw new NotSupportedException();

  public IQuicRpcServiceContext Context => throw new NotSupportedException();
}
