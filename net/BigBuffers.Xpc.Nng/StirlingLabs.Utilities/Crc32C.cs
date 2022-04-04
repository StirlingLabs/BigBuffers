#nullable enable
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Intrinsics.X86;
using ArmCrc32 = System.Runtime.Intrinsics.Arm.Crc32;
#endif

// TODO: move to StirlingLabs.Utilities
namespace StirlingLabs.Utilities
{
  public static class Crc32C
  {
    public const uint Poly = 0x82F63B78u;
    private static readonly uint[]? Table;
    static Crc32C()
    {
#if NET5_0_OR_GREATER
      if (Sse42.IsSupported || ArmCrc32.IsSupported)
        return;
#endif

      // if no hardware intrinsics, use lookup table
      var table = new uint[16 * 256];
      for (var i = 0u; i < 256; i++)
      {
        var res = i;
        for (var t = 0; t < 16; t++)
        {
          for (var k = 0; k < 8; k++)
          {
            res = (res & 1) == 1
              ? Poly ^ (res >> 1)
              : res >> 1;
          }

          table[t * 256 + i] = res;
        }
      }
      Table = table;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Calculate(ReadOnlySpan<byte> bytes)
      => Calculate(0u, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Calculate(uint checksum, ReadOnlySpan<byte> bytes)
    {
      var l = bytes.Length;
      ref var refByte = ref Unsafe.AsRef(bytes.GetPinnableReference());
      return Calculate(checksum, ref refByte, l);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Calculate(byte[] bytes)
      => Calculate(0u, bytes);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Calculate(uint checksum, byte[] bytes)
    {
      var l = bytes.Length;
#if NET5_0_OR_GREATER
      ref var refByte = ref MemoryMarshal.GetArrayDataReference(bytes);
#else
      ref var refByte = ref bytes[0];
#endif
      return Calculate(checksum, ref refByte, l);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Calculate(uint checksum, ref byte source, nint length)
    {
#if NET5_0_OR_GREATER
      if (Sse42.IsSupported)
        checksum = CalcSse42(checksum, ref source, length);
      else if (ArmCrc32.IsSupported)
        checksum = CalcArm(checksum, ref source, length);
      else
#endif
        return CalcNaive(checksum, ref source, length);
#if NET5_0_OR_GREATER
      return checksum;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CalcNaive(uint checksum, ref byte source, nint length)
    {
      var i = 0;
      var crcLocal = uint.MaxValue ^ checksum;
#if NET5_0_OR_GREATER
      ref var pTable = ref MemoryMarshal.GetArrayDataReference(Table!);
#else
      ref var pTable = ref Table![0];
#endif
      while (length >= 16)
      {
        var a = Unsafe.Add(ref pTable, (nint)(i + 3 * 256 + Unsafe.Add(ref source, i + 12))) ^
          Unsafe.Add(ref pTable, (nint)(i + 2 * 256 + Unsafe.Add(ref source, i + 13))) ^
          Unsafe.Add(ref pTable, (nint)(i + 1 * 256 + Unsafe.Add(ref source, i + 14))) ^
          Unsafe.Add(ref pTable, (nint)(i + 0 * 256 + Unsafe.Add(ref source, i + 15)));

        var b = Unsafe.Add(ref pTable, (nint)(i + 7 * 256 + Unsafe.Add(ref source, i + 8))) ^
          Unsafe.Add(ref pTable, (nint)(i + 6 * 256 + Unsafe.Add(ref source, i + 9))) ^
          Unsafe.Add(ref pTable, (nint)(i + 5 * 256 + Unsafe.Add(ref source, i + 10))) ^
          Unsafe.Add(ref pTable, (nint)(i + 4 * 256 + Unsafe.Add(ref source, i + 11)));

        var c = Unsafe.Add(ref pTable, (nint)(i + 11 * 256 + Unsafe.Add(ref source, i + 4))) ^
          Unsafe.Add(ref pTable, (nint)(i + 10 * 256 + Unsafe.Add(ref source, i + 5))) ^
          Unsafe.Add(ref pTable, (nint)(i + 9 * 256 + Unsafe.Add(ref source, i + 6))) ^
          Unsafe.Add(ref pTable, (nint)(i + 8 * 256 + Unsafe.Add(ref source, i + 7)));

        var d = Unsafe.Add(ref pTable, (nint)(i + 15 * 256 + ((byte)crcLocal ^ Unsafe.Add(ref source, i)))) ^
          Unsafe.Add(ref pTable, (nint)(i + 14 * 256 + ((byte)(crcLocal >> 8) ^ Unsafe.Add(ref source, i + 1)))) ^
          Unsafe.Add(ref pTable, (nint)(i + 13 * 256 + ((byte)(crcLocal >> 16) ^ Unsafe.Add(ref source, i + 2)))) ^
          Unsafe.Add(ref pTable, (nint)(12 * 256 + ((crcLocal >> 24) ^ Unsafe.Add(ref source, i + 3))));

        crcLocal = d ^ c ^ b ^ a;
        i += 16;
        length -= 16;
      }

      while (--length >= 0)
        crcLocal = Unsafe.Add(ref pTable, (nint)(byte)(crcLocal ^ Unsafe.Add(ref source, i++))) ^ (crcLocal >> 8);
      return crcLocal ^ uint.MaxValue;
    }

#if NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CalcSse42(uint checksum, ref byte source, nint length)
    {
      var i = 0;
      var crcLocal = uint.MaxValue ^ checksum;
      if (Sse42.X64.IsSupported)
      {
        for (; i <= length - 8; i += 8)
        {
          crcLocal = (uint)Sse42.X64.Crc32(crcLocal,
            Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, i)));
        }

        if (i <= length - 4)
        {
          crcLocal = Sse42.Crc32(crcLocal,
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i)));
          i += 4;
        }
      }
      else
        for (; i <= length - 4; i += 4)
        {
          crcLocal = Sse42.Crc32(crcLocal,
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i)));
        }

      if (i <= length - 2)
      {
        crcLocal = Sse42.Crc32(crcLocal,
          Unsafe.As<byte, ushort>(ref Unsafe.Add(ref source, i)));
        i += 2;
      }

      if (i < length)
        crcLocal = Sse42.Crc32(crcLocal, Unsafe.Add(ref source, i));
      return crcLocal ^ uint.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint CalcArm(uint checksum, ref byte source, nint length)
    {
      var i = 0;
      var crcLocal = uint.MaxValue ^ checksum;

      if (ArmCrc32.Arm64.IsSupported)
      {
        for (; i <= length - 8; i += 8)
        {
          crcLocal = ArmCrc32.Arm64.ComputeCrc32C(crcLocal,
            Unsafe.As<byte, ulong>(ref Unsafe.Add(ref source, i)));
        }
        if (i <= length - 4)
        {
          crcLocal = ArmCrc32.ComputeCrc32C(crcLocal,
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i)));
          i += 4;
        }
      }
      else
      {
        for (; i <= length - 4; i += 4)
        {
          crcLocal = ArmCrc32.ComputeCrc32C(crcLocal,
            Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, i)));
        }
      }

      if (i <= length - 2)
      {
        crcLocal = ArmCrc32.ComputeCrc32C(crcLocal,
          Unsafe.As<byte, ushort>(ref Unsafe.Add(ref source, i)));
        i += 2;
      }

      if (i < length)
        crcLocal = ArmCrc32.ComputeCrc32C(crcLocal, Unsafe.Add(ref source, i));

      return crcLocal ^ uint.MaxValue;
    }
#endif
  }
}
