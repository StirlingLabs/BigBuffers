using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_socket
  {
    public uint id;
  }
}