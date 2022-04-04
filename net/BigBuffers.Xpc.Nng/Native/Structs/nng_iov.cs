using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public unsafe struct nng_iov
  {
    public void* iov_buf;
    public nuint iov_len;
  }
}
