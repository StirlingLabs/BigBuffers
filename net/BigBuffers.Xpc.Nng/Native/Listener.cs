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
    public static extern int nng_listener_create(out nng_listener listener, nng_socket socket, Utf8String url);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_start(nng_listener listener, NngFlag flags);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_close(nng_listener listener);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_id(nng_listener listener);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get(nng_listener listener, Utf8String name, void* data, ref nuint size);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_bool(nng_listener listener, Utf8String name, out bool data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_int(nng_listener listener, Utf8String name, out int data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_ms(nng_listener listener, Utf8String name, out nng_duration data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_size(nng_listener listener, Utf8String name, out nuint data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_sockaddr(nng_listener listener, Utf8String name, out nng_sockaddr data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_string(nng_listener listener, Utf8String name, out Utf8String data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_uint64(nng_listener listener, Utf8String name, out ulong data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_get_ptr(nng_listener listener, Utf8String name, out void* data);


    public static byte[] nng_listener_get(nng_listener listener, Utf8String name)
    {
      nuint size = 0;
      var rc = nng_listener_get(listener, name, null, ref size);
      if (rc != 0) return null;
      var bytes = new byte[size];
      fixed (void* pBytes = bytes)
        return nng_listener_get(listener, name, pBytes, ref size) == 0 ? bytes : null;
    }
    
#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set(nng_listener listener, Utf8String name, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_listener_set(nng_listener listener, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_listener_set(listener, name, pData, (nuint)data.Length);
    }

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_bool(nng_listener listener, Utf8String name, bool value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_int(nng_listener listener, Utf8String name, int value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_ms(nng_listener listener, Utf8String name, nng_duration value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_size(nng_listener listener, Utf8String name, nuint value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_uint64(nng_listener listener, Utf8String name, ulong value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_ptr(nng_listener listener, Utf8String name, void* value);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listener_set_string(nng_listener listener, Utf8String name, Utf8String value);
  }
}
