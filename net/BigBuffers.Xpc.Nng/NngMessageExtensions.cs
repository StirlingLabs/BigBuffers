using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using nng;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  public static class NngMessageExtensions
  {
    public static ref readonly long Id(this in NngRequestMessage req)
    {
      ref readonly var a = ref req.Raw[0];
      return ref Unsafe.As<byte, long>(ref Unsafe.AsRef(a));
    }
    public static ref readonly long Id(in this NngReplyMessage rep)
    {
      ref readonly var start = ref rep.Raw[0];
      return ref Unsafe.As<byte, long>(ref Unsafe.AsRef(start));
    }
    public static ref readonly NngMessageType Type(in this NngRequestMessage req)
    {
      ref readonly var start = ref req.Raw[8];
      return ref Unsafe.As<byte, NngMessageType>(ref Unsafe.AsRef(start));
    }
    public static ref readonly NngMessageType Type(in this NngReplyMessage rep)
    {
      ref readonly var start = ref rep.Raw[8];
      return ref Unsafe.As<byte, NngMessageType>(ref Unsafe.AsRef(start));
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

    public static ReadOnlySpan<byte> Locator(in this NngRequestMessage req)
      => (req.Type() & NngMessageType.Continuation) != 0
        ? ReadOnlySpan<byte>.Empty
        : ReadNullTerminatedString(req.Raw, 16);

    public static ReadOnlySpan<byte> ServiceId(in this NngRequestMessage req)
    {
      var loc = req.Locator();
      var sepIndex = loc.IndexOf((byte)':');
      return sepIndex < 0 ? default : loc.Slice(0, sepIndex);
    }
    public static ReadOnlySpan<byte> ProcedureName(in this NngRequestMessage req)
    {
      var loc = req.Locator();
      return loc.Slice(1 + loc.IndexOf((byte)':'));
    }

    public static ReadOnlySpan<byte> Body(in this NngRequestMessage req)
    {
      if ((req.Type() & NngMessageType.Continuation) != 0)
        return req.Raw.Slice(16);

      var start = GetNullTerminatedStringLength(req.Raw, 16) + 16;
      const int align = 8;
      var alignBits = align - 1;
      start += (align - (start & alignBits)) & alignBits;
      return start >= req.Raw.Length
        ? ReadOnlySpan<byte>.Empty
        : req.Raw.Slice(start);
    }

    public static ReadOnlySpan<byte> Body(in this NngReplyMessage rep)
      => rep.Raw.Slice(16);

    public static int PadToAlignment(this INngMsg msg, int align, byte paddingByte = 0)
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static bool IsPow2(int value) => (value & (value - 1)) == 0 && value != 0;

      Debug.Assert(IsPow2(align));
      var alignBits = align - 1;
      var needed = (align - (msg.Length & alignBits)) & alignBits;
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      var paddingByteSpan = MemoryMarshal.CreateReadOnlySpan(ref paddingByte, 1);
#else
      ReadOnlySpan<byte> paddingByteSpan = stackalloc[] { paddingByte };
#endif
      for (var i = 0; i < needed; ++i)
        msg.Append(paddingByteSpan);
      return needed;
    }

    public static NngRequestMessage ParseRequest(this INngMsg msg) => new(msg);

    public static NngReplyMessage ParseReply(this INngMsg msg) => new(msg);

    public static long Id(this INngMsg msg)
      => MemoryMarshal.Read<long>(msg.AsSpan().Slice(0, 8));
  }
}
