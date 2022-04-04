using System.Runtime.InteropServices;

namespace NngNative
{
  /// <summary>
  /// This is an alias of <see cref="nng_sockaddr_path"/>. 
  /// </summary>
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_ipc
  {
    public nng_sockaddr_path path;
  }
}
