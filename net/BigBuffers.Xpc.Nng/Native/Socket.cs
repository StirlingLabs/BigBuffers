using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{
  public static unsafe partial class LibNng
  {
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_close(nng_socket socket);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_dial(nng_socket socket, Utf8String url, out nng_dialer dialer, NngFlag flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_dial(nng_socket socket, Utf8String url, IntPtr not_used, NngFlag flags);

    public static int nng_dial(nng_socket socket, Utf8String url, NngFlag flags)
      => nng_dial(socket, url, IntPtr.Zero, flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_listen(nng_socket socket, Utf8String url, out nng_listener listener, NngFlag flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_listen(nng_socket socket, Utf8String url, IntPtr not_used, NngFlag flags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_listen(nng_socket socket, Utf8String url, NngFlag flags)
      => nng_listen(socket, url, IntPtr.Zero, flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_id(nng_socket socket);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_socket_set(nng_socket socket, Utf8String name, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_socket_set(nng_socket socket, Utf8String name, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_socket_set(socket, name, pData, (nuint)data.Length);
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_bool(nng_socket socket, Utf8String name, bool value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_int(nng_socket socket, Utf8String name, int value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_ms(nng_socket socket, Utf8String name, nng_duration value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_size(nng_socket socket, Utf8String name, nuint value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_uint64(nng_socket socket, Utf8String name, ulong value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_string(nng_socket socket, Utf8String name, Utf8String value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_set_ptr(nng_socket socket, Utf8String name, IntPtr value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get(nng_socket socket, Utf8String name, void* data, ref nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_bool(nng_socket socket, Utf8String name, out bool value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_int(nng_socket socket, Utf8String name, out int value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_ms(nng_socket socket, Utf8String name, out nng_duration value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_size(nng_socket socket, Utf8String name, out nuint value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_string(nng_socket socket, Utf8String name, out Utf8String value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_uint64(nng_socket socket, Utf8String name, out ulong value);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_socket_get_ptr(nng_socket socket, Utf8String name, out void* value);

    public static byte[] nng_socket_get(nng_socket socket, Utf8String name)
    {
      nuint size = 0;
      var rc = nng_socket_get(socket, name, null, ref size);
      if (rc != 0) return null;
      var bytes = new byte[size];
      fixed (void* pBytes = bytes)
        return nng_socket_get(socket, name, pBytes, ref size) == 0 ? bytes : null;
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int nng_send(nng_socket socket, IntPtr data, nuint size, NngFlag flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_recv(nng_socket socket, ref IntPtr data, ref nuint size, NngFlag flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_sendmsg(nng_socket socket, nng_msg* message, NngFlag flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_recvmsg(nng_socket socket, out nng_msg* message, NngFlag flags);
  }
}
