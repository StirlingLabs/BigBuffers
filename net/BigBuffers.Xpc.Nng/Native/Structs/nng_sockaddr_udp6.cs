using System.Runtime.InteropServices;

namespace NngNative
{
  /// <summary>
  /// In nng this is a typedef:
  /// typedef struct nng_sockaddr_in6 nng_sockaddr_udp6;
  /// </summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_udp6
  {
    public nng_sockaddr_in6 sockaddr_path;
  }
}