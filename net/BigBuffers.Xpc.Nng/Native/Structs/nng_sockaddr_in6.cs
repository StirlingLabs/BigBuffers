using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NngNative
{
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_sockaddr_in6
  {
    public nng_sockaddr_family sa_family;
    public ushort sa_port;

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    internal struct blob_16_bytes { }

    internal blob_16_bytes sa_addr_;
    
    public uint sa_scope;
  };

  public static unsafe partial class Extensions
  {
    public static Span<byte> sa_addr(ref this nng_sockaddr_in6 p)
#if NETSTANDARD2_0
      => new(Unsafe.AsPointer(ref p.sa_addr_), 16);
#else
      => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref p.sa_addr_, 1));
#endif
  }
}
