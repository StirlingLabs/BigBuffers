using System.Runtime.CompilerServices;

namespace BigBuffers
{
  internal struct ByteBufferResidentModel
  {
    public ulong Offset;

    public ByteBuffer ByteBuffer;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe ref ByteBufferResidentModel UnsafeSelfReference()
      => ref Unsafe.AsRef<ByteBufferResidentModel>(Unsafe.AsPointer(ref Unsafe.AsRef(this)));
  }
}
