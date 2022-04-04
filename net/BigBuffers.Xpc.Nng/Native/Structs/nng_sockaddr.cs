using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Explicit)]
  public struct nng_sockaddr
  {
    [FieldOffset(0)]
    public nng_sockaddr_family s_family;
    [FieldOffset(0)]
    public nng_sockaddr_ipc s_ipc;
    [FieldOffset(0)]
    public nng_sockaddr_inproc s_inproc;
    [FieldOffset(0)]
    public nng_sockaddr_in6 s_in6;
    [FieldOffset(0)]
    public nng_sockaddr_in s_in;
    [FieldOffset(0)]
    public nng_sockaddr_zt s_zt;
  };
}