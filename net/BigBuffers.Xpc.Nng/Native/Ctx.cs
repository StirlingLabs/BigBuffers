using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{

  public static unsafe partial class LibNng
  {
#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_open(out nng_ctx ctx, nng_socket socket);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_close(nng_ctx ctx);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_id(nng_ctx ctx);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_ctx_recv(nng_ctx ctx, nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_ctx_send(nng_ctx ctx, nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get(nng_ctx ctx, Utf8String name, void* data, ref nuint size);

    public static byte[] nng_ctx_get(nng_ctx ctx, Utf8String name)
    {
      nuint size = 0;
      var rc = nng_ctx_get(ctx, name, null, ref size);
      if (rc != 0 || size == 0) return null;
      var bytes = new byte[size];
      fixed (void* pBytes = bytes)
        return nng_ctx_get(ctx, name, pBytes, ref size) == 0 ? bytes : null;
    }

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_bool(nng_ctx ctx, Utf8String name, out bool data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_int(nng_ctx ctx, Utf8String name, out int data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_ms(nng_ctx ctx, Utf8String name, out nng_duration data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_ptr(nng_ctx ctx, Utf8String name, out void* data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_size(nng_ctx ctx, Utf8String name, out nuint data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_string(nng_ctx ctx, Utf8String name, out Utf8String data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_get_uint64(nng_ctx ctx, Utf8String name, out ulong data);


#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set(nng_ctx ctx, Utf8String name, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_ctx_set(nng_ctx ctx, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_ctx_set(ctx, name, pData, (nuint)data.Length);
    }

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_bool(nng_ctx ctx, Utf8String name, bool value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_int(nng_ctx ctx, Utf8String name, int value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_ms(nng_ctx ctx, Utf8String name, nng_duration value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_ptr(nng_ctx ctx, Utf8String name, void* value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_size(nng_ctx ctx, Utf8String name, nuint value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_string(nng_ctx ctx, Utf8String name, Utf8String value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_ctx_set_uint64(nng_ctx ctx, Utf8String name, ulong value);
  }
}
