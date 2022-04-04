using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_inproc
  {
    public ushort sa_family;
    char_maxaddrlen_blob sa_name;
  };
}