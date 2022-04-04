using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_in
  {
    public nng_sockaddr_family sa_family;
    public ushort sa_port;
    public uint sa_addr;
  };
}