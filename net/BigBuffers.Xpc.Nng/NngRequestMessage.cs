using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using nng;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  public readonly ref struct NngRequestMessage
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NngRequestMessage(INngMsg msg) : this(msg.AsSpan()) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NngRequestMessage(ReadOnlySpan<byte> raw) => Raw = raw;

    public readonly ReadOnlySpan<byte> Raw;
  }
}
