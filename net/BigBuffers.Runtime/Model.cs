using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  public readonly struct Model : IEquatable<Model>
  {
    public readonly ulong Offset;

    public readonly ByteBuffer ByteBuffer;

    public Model(ByteBuffer byteBuffer, ulong offset)
    {
      Debug.Assert(offset <= long.MaxValue);
      ByteBuffer = byteBuffer;
      Offset = offset;
    }
    public bool Equals(Model other)
      => Offset == other.Offset && ByteBuffer.Equals(other.ByteBuffer);

    public override bool Equals(object obj)
      => obj is Model other && Equals(other);

    public override int GetHashCode()
    {
      unchecked
      { 
        return (Offset.GetHashCode() * 397) ^ ByteBuffer.GetHashCode();
      }
    }

    public static bool operator ==(Model left, Model right)
      => left.Equals(right);

    public static bool operator !=(Model left, Model right)
      => !left.Equals(right);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ref Model UnsafeSelfReference()
      => ref Unsafe.AsRef<Model>(Unsafe.AsPointer(ref Unsafe.AsRef(this)));
  }
}
