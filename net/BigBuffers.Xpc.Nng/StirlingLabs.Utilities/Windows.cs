using System;
using System.Runtime.InteropServices;

// TODO: move to StirlingLabs.Utilities
namespace StirlingLabs.Utilities
{
  internal static class Windows
  {
    private const string UCrt = "ucrtbase";

    [DllImport(UCrt, EntryPoint = "malloc", SetLastError = true)]
    public static extern IntPtr malloc(nuint size);

    [DllImport(UCrt, EntryPoint = "calloc", SetLastError = true)]
    public static extern IntPtr calloc(nuint number, nuint size);

    [DllImport(UCrt, EntryPoint = "free", SetLastError = true)]
    public static extern void free(IntPtr ptr);
  }
}
