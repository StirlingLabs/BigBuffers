using System;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public readonly struct RawMemory<T> where T : unmanaged
{
  public readonly nuint Length;
  public readonly unsafe T* Pointer;
  public unsafe nuint ByteSize => Length * (nuint)sizeof(T);

  public unsafe RawMemory(T* pointer, nuint length)
  {
    Pointer = pointer;
    Length = length;
  }

  public static unsafe implicit operator ReadOnlySpan<T>(RawMemory<T> m)
    => new(m.Pointer, (int)m.Length);

  public static unsafe implicit operator ReadOnlyBigSpan<T>(RawMemory<T> m)
    => new(m.Pointer, m.Length);

  public static unsafe implicit operator Span<T>(RawMemory<T> m)
    => new(m.Pointer, (int)m.Length);

  public static unsafe implicit operator BigSpan<T>(RawMemory<T> m)
    => new(m.Pointer, m.Length);
}
