using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  [PublicAPI]
  public abstract class ByteBufferAllocator : IEquatable<ByteBufferAllocator>
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

    public abstract byte[] Buffer
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get;
    }

    public uint Length => (uint)Buffer.Length;

    public ulong LongLength => (ulong)Buffer.LongLength;

    public abstract void GrowFront(ulong newSize);

    public bool Equals(ByteBufferAllocator other)
      => !ReferenceEquals(null, other)
        && (ReferenceEquals(this, other)
          || Equals(Buffer, other.Buffer));

    public override bool Equals(object obj)
      => !ReferenceEquals(null, obj)
        && (ReferenceEquals(this, obj)
          || this.TypeEquals(obj)
          && Equals((ByteBufferAllocator)obj));

    public override int GetHashCode()
      => Buffer is null ? 0 : Buffer.GetHashCode();

    public static bool operator ==(ByteBufferAllocator left, ByteBufferAllocator right)
      => left?.Equals(right) ?? right is null;

    public static bool operator !=(ByteBufferAllocator left, ByteBufferAllocator right)
      => !left?.Equals(right) ?? right is not null;
  }
}
