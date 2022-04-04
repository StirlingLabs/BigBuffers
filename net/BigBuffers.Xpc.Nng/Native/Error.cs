using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{
  public static partial class LibNng
  {
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Utf8String nng_strerror(int error);
  }
}
