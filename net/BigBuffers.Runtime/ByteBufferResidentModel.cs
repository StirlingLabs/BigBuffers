using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BigBuffers
{
  [StructLayout(LayoutKind.Sequential)]
  internal readonly struct ByteBufferResidentModel : IEquatable<ByteBufferResidentModel>
  {
    public readonly ulong Offset;

    public readonly ByteBuffer ByteBuffer;


    public ByteBufferResidentModel(ByteBuffer byteBuffer, ulong offset)
    {
      ByteBuffer = byteBuffer;
      Offset = offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe ref ByteBufferResidentModel UnsafeSelfReference()
      => ref Unsafe.AsRef<ByteBufferResidentModel>(Unsafe.AsPointer(ref Unsafe.AsRef(this)));
    public bool Equals(ByteBufferResidentModel other)
      => Offset == other.Offset && ByteBuffer.Equals(other.ByteBuffer);

    public override bool Equals(object obj)
      => obj is ByteBufferResidentModel other && Equals(other);

    public override int GetHashCode()
    {
      unchecked
      {
        return (Offset.GetHashCode() * 397) ^ ByteBuffer.GetHashCode();
      }
    }
    public static bool operator ==(ByteBufferResidentModel left, ByteBufferResidentModel right)
      => left.Equals(right);

    public static bool operator !=(ByteBufferResidentModel left, ByteBufferResidentModel right)
      => !left.Equals(right);
  }
}
