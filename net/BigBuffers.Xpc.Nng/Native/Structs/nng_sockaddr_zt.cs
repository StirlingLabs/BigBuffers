using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_zt
  {
    public nng_sockaddr_family sa_family;
    public ulong sa_nwid;
    public ulong sa_nodeid;
    public uint sa_port;
  };
}