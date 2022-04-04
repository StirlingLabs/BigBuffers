using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_path
  {
    public nng_sockaddr_family sa_family;
    internal char_maxaddrlen_blob sa_path_;
  };

  public static unsafe partial class Extensions
  {
    public static Span<byte> sa_path(ref this nng_sockaddr_path p)
#if NETSTANDARD2_0
      => new(Unsafe.AsPointer(ref p.sa_path_), 16);
#else
      => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref p.sa_path_, 1));
#endif
  }
}
