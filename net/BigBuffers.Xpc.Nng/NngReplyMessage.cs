using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using nng;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  public readonly ref struct NngReplyMessage
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NngReplyMessage(INngMsg msg) : this(msg.AsSpan()) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NngReplyMessage(ReadOnlySpan<byte> raw) => Raw = raw;

    public readonly ReadOnlySpan<byte> Raw;
  }
}
