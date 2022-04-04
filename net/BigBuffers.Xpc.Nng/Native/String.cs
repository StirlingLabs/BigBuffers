using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{
  public static partial class LibNng
  {
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Utf8String nng_version();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Utf8String nng_strdup(Utf8String str);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_strfree(Utf8String str);
  }
}
