using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  [PublicAPI]
  public abstract class ByteBufferManager : IEquatable<ByteBufferManager>
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

    public uint Length => (uint)Span.Length;

    public ulong LongLength => Span.LongLength;

    public abstract bool Growable { get; set; }

    public abstract void GrowFront(ulong newSize);

    public bool Equals(ByteBufferManager other)
      => !ReferenceEquals(null, other)
        && (ReferenceEquals(this, other)
          || Span == other.Span);

    public override bool Equals(object obj)
      => !ReferenceEquals(null, obj)
        && (ReferenceEquals(this, obj)
          || this.TypeEquals(obj)
          && Equals((ByteBufferManager)obj));

    public override unsafe int GetHashCode()
      => Span.IsEmpty ? 0 : ((IntPtr)Span.GetUnsafePointer()).GetHashCode();

    public static bool operator ==(ByteBufferManager left, ByteBufferManager right)
      => left?.Equals(right) ?? right is null;

    public static bool operator !=(ByteBufferManager left, ByteBufferManager right)
      => !left?.Equals(right) ?? right is not null;

    public virtual bool TryGetMemory(out Memory<byte> seg)
    {
      Unsafe.SkipInit(out seg);
      return false;
    }
  }
}
