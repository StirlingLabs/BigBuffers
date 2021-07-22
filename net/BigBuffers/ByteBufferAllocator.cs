using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public abstract class ByteBufferAllocator
  {
    public abstract BigSpan<byte> Span
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get;
    }

    public abstract ReadOnlyBigSpan<byte> ReadOnlySpan
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get;
    }

    public byte[] Buffer
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get;
      protected set;
    }

    public uint Length
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => checked((uint)LongLength);
    }

    public ulong LongLength
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get;
      protected set;
    }

    public abstract void GrowFront(ulong newSize);
  }
}
