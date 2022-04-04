using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void PipeEventCallback(nng_pipe pipe, NngPipeEv ev, IntPtr arg);
}