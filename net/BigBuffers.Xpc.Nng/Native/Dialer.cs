using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{

  public static unsafe partial class LibNng
  {
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_create(out nng_dialer dialer, nng_socket socket, Utf8String url);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_start(nng_dialer dialer, NngFlag flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_close(nng_dialer dialer);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_id(nng_dialer dialer);


    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get(nng_dialer dialer, Utf8String name, void* data, ref nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_bool(nng_dialer dialer, Utf8String name, out bool data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_int(nng_dialer dialer, Utf8String name, out int data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_ms(nng_dialer dialer, Utf8String name, out nng_duration data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_ptr(nng_dialer dialer, Utf8String name, out void* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_size(nng_dialer dialer, Utf8String name, out nuint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_string(nng_dialer dialer, Utf8String name, out Utf8String data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_get_uint64(nng_dialer dialer, Utf8String name, out ulong data);

    public static byte[] nng_dialer_get(nng_dialer dialer, Utf8String name)
    {
      nuint size = 0;
      var rc = nng_dialer_get(dialer, name, null, ref size);
      if (rc != 0) return null;
      var bytes = new byte[size];
      fixed (void* pBytes = bytes)
        return nng_dialer_get(dialer, name, pBytes, ref size) == 0 ? bytes : null;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_dialer_set(nng_dialer dialer, Utf8String name, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_dialer_set(nng_dialer dialer, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_dialer_set(dialer, name, pData, (nuint)data.Length);
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_bool(nng_dialer dialer, Utf8String name, bool value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_int(nng_dialer dialer, Utf8String name, int value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_ms(nng_dialer dialer, Utf8String name, nng_duration value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_ptr(nng_dialer dialer, Utf8String name, IntPtr value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_size(nng_dialer dialer, Utf8String name, nuint value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_string(nng_dialer dialer, Utf8String name, Utf8String value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dialer_set_uint64(nng_dialer dialer, Utf8String name, ulong value);
  }
}
