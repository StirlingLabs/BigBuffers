using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.Shm
{
  [PublicAPI]
  public static class ShmMessageExtensions
  {
    public static ref readonly long Id(this in ShmRequestMessage req)
    {
      ref readonly var a = ref req.Raw[0];
      return ref Unsafe.As<byte, long>(ref Unsafe.AsRef(a));
    }
    public static ref readonly long Id(in this ShmReplyMessage rep)
    {
      ref readonly var start = ref rep.Raw[0];
      return ref Unsafe.As<byte, long>(ref Unsafe.AsRef(start));
    }
    public static ref readonly ShmMessageType Type(in this ShmRequestMessage req)
    {
      ref readonly var start = ref req.Raw[8];
      return ref Unsafe.As<byte, ShmMessageType>(ref Unsafe.AsRef(start));
    }
    public static ref readonly ShmMessageType Type(in this ShmReplyMessage rep)
    {
      ref readonly var start = ref rep.Raw[8];
      return ref Unsafe.As<byte, ShmMessageType>(ref Unsafe.AsRef(start));
    }

    private static int FindByteOffset(ReadOnlySpan<byte> span, int start, byte value)
      => span.Slice(start).IndexOf(value);

    private static int GetNullTerminatedStringLength(ReadOnlySpan<byte> span, int start)
      => FindByteOffset(span, start, 0);

    private static ReadOnlySpan<byte> ReadNullTerminatedString(ReadOnlySpan<byte> span, int startIndex)
    {
      ref readonly var start = ref span[startIndex];
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(start),
        GetNullTerminatedStringLength(span, startIndex));
#else
      unsafe
      {
        return new Span<byte>(Unsafe.AsPointer(ref Unsafe.AsRef(start)),
          GetNullTerminatedStringLength(span, startIndex));
      }
#endif
    }

    public static ReadOnlySpan<byte> Locator(in this ShmRequestMessage req)
      => (req.Type() & ShmMessageType.Continuation) != 0
        ? ReadOnlySpan<byte>.Empty
        : ReadNullTerminatedString(req.Raw, 16);

    public static ReadOnlySpan<byte> ServiceId(in this ShmRequestMessage req)
    {
      var loc = req.Locator();
      var sepIndex = loc.IndexOf((byte)':');
      return sepIndex < 0 ? default : loc.Slice(0, sepIndex);
    }
    public static ReadOnlySpan<byte> ProcedureName(in this ShmRequestMessage req)
    {
      var loc = req.Locator();
      return loc.Slice(1 + loc.IndexOf((byte)':'));
    }

    public static ReadOnlySpan<byte> Body(in this ShmRequestMessage req)
    {
      if ((req.Type() & ShmMessageType.Continuation) != 0)
        return req.Raw.Slice(16);

      var start = GetNullTerminatedStringLength(req.Raw, 16) + 16;
      const int align = 8;
      var alignBits = align - 1;
      start += (align - (start & alignBits)) & alignBits;
      return start >= req.Raw.Length
        ? ReadOnlySpan<byte>.Empty
        : req.Raw.Slice(start);
    }

    public static ReadOnlySpan<byte> Body(in this ShmReplyMessage rep)
      => rep.Raw.Slice(16);


    public static ShmRequestMessage ParseRequest(this ReadOnlySpan<byte> msg) => new(msg);

    public static ShmReplyMessage ParseReply(this ReadOnlySpan<byte> msg) => new(msg);

    internal static long Id(this ReadOnlySpan<byte> msg)
      => MemoryMarshal.Read<long>(msg.Slice(0, 8));
  }
}
