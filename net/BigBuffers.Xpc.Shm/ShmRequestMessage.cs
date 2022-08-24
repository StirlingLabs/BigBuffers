using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.Shm
{
  [PublicAPI]
  public readonly ref struct ShmRequestMessage
  {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ShmRequestMessage(ReadOnlySpan<byte> raw) => Raw = raw;

    public readonly ReadOnlySpan<byte> Raw;
  }
}
