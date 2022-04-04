using System;
using System.Runtime.InteropServices;

// TODO: move to StirlingLabs.Utilities
namespace StirlingLabs.Utilities
{
  internal static class Posix
  {
    private const string LibC = "libc";

    [DllImport(LibC, EntryPoint = "malloc", SetLastError = true)]
    public static extern IntPtr malloc(nuint size);

    [DllImport(LibC, EntryPoint = "calloc", SetLastError = true)]
    public static extern IntPtr calloc(nuint number, nuint size);

    [DllImport(LibC, EntryPoint = "free", SetLastError = true)]
    public static extern void free(IntPtr ptr);
  }
}
