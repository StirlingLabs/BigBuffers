using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

// TODO: move to StirlingLabs.Utilities
namespace StirlingLabs.Utilities
{
  [PublicAPI]
  public static partial class NativeMemory
  {
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static readonly NativeAllocDelegate malloc;
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static readonly NativeCAllocDelegate calloc;
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private static readonly NativeFreeDelegate free;

    static NativeMemory()
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        malloc = Windows.malloc;
        calloc = Windows.calloc;
        free = Windows.free;
      }
      else
      {
        malloc = Posix.malloc;
        calloc = Posix.calloc;
        free = Posix.free;
      }
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr Alloc(nuint size)
      => calloc(1, size);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr UnsafeAlloc(nuint size)
      => malloc(size);

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Free(IntPtr ptr)
      => free(ptr);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int GetByteStringLength(sbyte* p, int max)
    {
      // TODO: alignment-lead-up to large aligned reads, no unaligned page faults
      for (var i = 0; i < max; ++i)
      {
        if (p[i] == 0)
          return i;
      }
      return max;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe nuint GetByteStringLength(sbyte* p, nuint max)
    {
      // TODO: alignment-lead-up to large aligned reads, no unaligned page faults
      for (nuint i = 0; i < max; ++i)
      {
        if (p[i] == 0)
          return i;
      }
      return max;
    }
  }

  public delegate IntPtr NativeAllocDelegate(nuint size);

  public delegate IntPtr NativeCAllocDelegate(nuint number, nuint size);

  public delegate void NativeFreeDelegate(IntPtr ptr);
}
