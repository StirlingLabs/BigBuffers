using System;
using System.Runtime.InteropServices;

namespace NngNative
{
  using static LibNng;
  [StructLayout(LayoutKind.Sequential)]
  public struct nng_duration
  {
    public int TimeMs { get; set; }

    public nng_duration(nng_duration copy)
    {
      TimeMs = copy.TimeMs;
    }
    
    public static readonly nng_duration Infinite = new() { TimeMs = NNG_DURATION_INFINITE };
    public static readonly nng_duration Default = new() { TimeMs = NNG_DURATION_DEFAULT };
    public static readonly nng_duration Zero = new() { TimeMs = NNG_DURATION_ZERO };

    public static nng_duration operator +(nng_duration lhs, nng_duration rhs) =>
      new()
        { TimeMs = lhs.TimeMs + rhs.TimeMs };

    public static nng_duration operator +(nng_duration lhs, int rhs) =>
      new()
        { TimeMs = lhs.TimeMs + rhs };
  }
}
