using System;
using System.Runtime.InteropServices;

namespace BigBuffers.Nng.Native
{
  public static class Posix
  {
    [DllImport("libc", EntryPoint = "malloc", SetLastError = true)]
    public static extern IntPtr malloc(nuint size);

    [DllImport("libc", EntryPoint = "calloc", SetLastError = true)]
    public static extern IntPtr calloc(nuint number, nuint size);

    [DllImport("libc", EntryPoint = "free", SetLastError = true)]
    public static extern void free(IntPtr ptr);
  }
}
