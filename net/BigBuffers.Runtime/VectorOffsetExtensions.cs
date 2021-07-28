using System.Runtime.CompilerServices;

namespace BigBuffers
{
  public static class VectorOffsetExtensions
  {
    public static unsafe ulong Resolve<TVectorOffset>(ref this TVectorOffset v)
      where TVectorOffset : struct, IVectorOffset
      => *(ulong*)Unsafe.AsPointer(ref v);
    public static unsafe ulong Resolve<TVectorOffset>(ref this TVectorOffset v, ulong relativeTo)
      where TVectorOffset : struct, IVectorOffset
      => relativeTo - *(ulong*)Unsafe.AsPointer(ref v);
  }
}
