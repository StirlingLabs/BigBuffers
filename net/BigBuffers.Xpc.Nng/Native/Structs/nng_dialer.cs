using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_dialer
  {
    public uint id;
  }
}
