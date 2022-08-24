using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.Shm
{
  [PublicAPI]
  public readonly ref struct ShmReplyMessage
  {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShmReplyMessage(ReadOnlySpan<byte> raw) => Raw = raw;

    public readonly ReadOnlySpan<byte> Raw;
  }
}
