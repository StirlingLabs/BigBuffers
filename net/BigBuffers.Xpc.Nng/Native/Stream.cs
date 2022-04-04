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
    public static extern void nng_stream_free(nng_stream* stream);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_close(nng_stream* stream);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_send(nng_stream* stream, nng_aio* aio);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_recv(nng_stream* stream, nng_aio* aio);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set(nng_stream* stream, Utf8String name, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_stream_set(nng_stream* stream, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_stream_set(stream, name, pData, (nuint)data.Length);
    }

    // [DllImport(NngDll, CallingConvention = CallingConvention.Cdecl)]
    // public static extern Int32 nng_stream_get(nng_stream* stream, Utf8String name, void *, size_t *);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_bool(nng_stream* stream, Utf8String name, out bool data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_int(nng_stream* stream, Utf8String name, out int data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_ms(nng_stream* stream, Utf8String name, out nng_duration data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_size(nng_stream* stream, Utf8String name, out nuint data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_uint64(nng_stream* stream, Utf8String name, out ulong data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_string(nng_stream* stream, Utf8String name, out Utf8String data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_get_ptr(nng_stream* stream, Utf8String name, out void* data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_get_addr(nng_stream* stream, Utf8String name, out nng_sockaddr* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_bool(nng_stream* stream, Utf8String name, bool value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_int(nng_stream* stream, Utf8String name, int value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_ms(nng_stream* stream, Utf8String name, nng_duration value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_size(nng_stream* stream, Utf8String name, nuint value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_uint64(nng_stream* stream, Utf8String name, ulong value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_string(nng_stream* stream, Utf8String name, Utf8String value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_set_ptr(nng_stream* stream, Utf8String name, void* value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_set_addr(nng_stream* stream, Utf8String name, nng_sockaddr* value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_alloc(nng_stream_dialer** dialer, Utf8String addr);
    // [DllImport(NngDll, CallingConvention = CallingConvention.Cdecl)]
    // public static extern Int32 nng_stream_dialer_alloc_url(nng_stream_dialer** dialer, const nng_url *);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_dialer_free(nng_stream_dialer* dialer);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_dialer_close(nng_stream_dialer* dialer);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_dialer_dial(nng_stream_dialer* dialer, nng_aio* aio);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set(nng_stream_dialer* dialer, Utf8String name, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_stream_dialer_set(nng_stream_dialer* dialer, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_stream_dialer_set(dialer, name, pData, (nuint)data.Length);
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_dialer_get(nng_stream_dialer* dialer, Utf8String name, void* data, nuint* length);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_bool(nng_stream_dialer* dialer, Utf8String name, out bool data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_int(nng_stream_dialer* dialer, Utf8String name, out int data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_ms(nng_stream_dialer* dialer, Utf8String name, out nng_duration data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_size(nng_stream_dialer* dialer, Utf8String name, out nuint data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_uint64(nng_stream_dialer* dialer, Utf8String name, out ulong data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_string(nng_stream_dialer* dialer, Utf8String name, out Utf8String data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_get_ptr(nng_stream_dialer* dialer, Utf8String name, out void* data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_dialer_get_addr(nng_stream_dialer* dialer, Utf8String name, out nng_sockaddr* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_bool(nng_stream_dialer* dialer, Utf8String name, bool value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_int(nng_stream_dialer* dialer, Utf8String name, int value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_ms(nng_stream_dialer* dialer, Utf8String name, nng_duration value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_size(nng_stream_dialer* dialer, Utf8String name, nuint value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_uint64(nng_stream_dialer* dialer, Utf8String name, ulong value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_string(nng_stream_dialer* dialer, Utf8String name, Utf8String value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_dialer_set_ptr(nng_stream_dialer* dialer, Utf8String name, void* value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_dialer_set_addr(nng_stream_dialer* dialer, Utf8String name, nng_sockaddr* value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_alloc(nng_stream_listener** listener, Utf8String url);
    // [DllImport(NngDll, CallingConvention = CallingConvention.Cdecl)]
    // public static extern Int32 nng_stream_listener_alloc_url(ref nng_stream_listener* listener, const nng_url *);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_listener_free(nng_stream_listener* listener);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_listener_close(nng_stream_listener* listener);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_listen(nng_stream_listener* listener);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stream_listener_accept(nng_stream_listener* listener, nng_aio* aio);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set(nng_stream_listener* listener, Utf8String name, void* data, nuint size);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_stream_listener_set(nng_stream_listener* listener, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_stream_listener_set(listener, name, pData, (nuint)data.Length);
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_listener_get(nng_stream_listener* listener, Utf8String name, void* data, nuint* length);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_bool(nng_stream_listener* listener, Utf8String name, out bool data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_int(nng_stream_listener* listener, Utf8String name, out int data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_ms(nng_stream_listener* listener, Utf8String name, out nng_duration data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_size(nng_stream_listener* listener, Utf8String name, out nuint data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_uint64(nng_stream_listener* listener, Utf8String name, out ulong data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_string(nng_stream_listener* listener, Utf8String name, out Utf8String data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_get_ptr(nng_stream_listener* listener, Utf8String name, out void* data);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_listener_get_addr(nng_stream_listener* listener, Utf8String name, out nng_sockaddr* data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_bool(nng_stream_listener* listener, Utf8String name, bool value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_int(nng_stream_listener* listener, Utf8String name, int value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_ms(nng_stream_listener* listener, Utf8String name, nng_duration value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_size(nng_stream_listener* listener, Utf8String name, nuint value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_uint64(nng_stream_listener* listener, Utf8String name, ulong value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_string(nng_stream_listener* listener, Utf8String name, Utf8String value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stream_listener_set_ptr(nng_stream_listener* listener, Utf8String name, void* value);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern Int32 nng_stream_listener_set_addr(nng_stream_listener* listener, Utf8String name, nng_sockaddr* value);
  }
}
