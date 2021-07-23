using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BigBuffers
{
  [StructLayout(LayoutKind.Sequential)]
  internal struct ByteBufferResidentModel
  {
    public ulong Offset;

    public ByteBuffer ByteBuffer;


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe ref ByteBufferResidentModel UnsafeSelfReference()
      => ref Unsafe.AsRef<ByteBufferResidentModel>(Unsafe.AsPointer(ref Unsafe.AsRef(this)));
  }
}
