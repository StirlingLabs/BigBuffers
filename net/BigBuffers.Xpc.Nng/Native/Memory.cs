using System;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{
  public static partial class LibNng
  {
    public const string LibName = "libnng";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void* nng_alloc(nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void nng_free(void* ptr, nuint size);
  }
}
