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
    public static extern int nng_msg_alloc(out nng_msg* message, nuint size = 0);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_msg_free(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_realloc(ref nng_msg* message, nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void* nng_msg_header(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint nng_msg_header_len(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe void* nng_msg_body(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint nng_msg_len(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int nng_msg_append(nng_msg* message, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_msg_append(nng_msg* message, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_msg_append(message, pData, (nuint)data.Length);
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_msg_insert(nng_msg* message, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_msg_insert(nng_msg* message, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_msg_insert(message, pData, (nuint)data.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_msg_prepend(nng_msg* message, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_msg_prepend(message, pData, (nuint)data.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_msg_prepend(nng_msg* message, void* data, nuint size)
      => nng_msg_insert(message, data, size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_trim(nng_msg* message, nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_chop(nng_msg* message, nuint size);

    // [DllImport(NngDll, CallingConvention = CallingConvention.Cdecl)]
    // static extern Int32 nng_msg_header_append(nng_msg* message, byte[] data, nuint size);

    // public static Int32 nng_msg_header_append(nng_msg* message, byte[] data)
    // {
    //     return nng_msg_header_append(message, data, (nuint)data.Length);
    // }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe int nng_msg_header_append(nng_msg* message, void* data, nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_msg_header_insert(nng_msg* message, void* data, nuint size);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int nng_msg_header_insert(nng_msg* message, ReadOnlySpan<byte> data)
    {
      fixed (void* pData = data)
        return nng_msg_header_insert(message, pData, (nuint)data.Length);
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_header_trim(nng_msg* message, nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_header_chop(nng_msg* message, nuint size);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_header_append_u32(nng_msg* message, uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_header_insert_u32(nng_msg* message, uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_header_chop_u32(nng_msg* message, out uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_header_trim_u32(nng_msg* message, out uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_append_u32(nng_msg* message, uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_insert_u32(nng_msg* message, uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_chop_u32(nng_msg* message, out uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_trim_u32(nng_msg* message, out uint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_msg_dup(out nng_msg* dest, nng_msg* source);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_msg_clear(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_msg_header_clear(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_msg_set_pipe(nng_msg* message, nng_pipe pipe);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_pipe nng_msg_get_pipe(nng_msg* message);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_close(nng_pipe pipe);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_id(nng_pipe pipe);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_socket nng_pipe_socket(nng_pipe pipe);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_dialer nng_pipe_dialer(nng_pipe pipe);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_listener nng_pipe_listener(nng_pipe pipe);
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_notify(nng_socket socket, NngPipeEv ev, PipeEventCallback callback, IntPtr arg);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_bool(nng_pipe pipe, Utf8String opt, out bool val);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_int(nng_pipe pipe, Utf8String opt, out int val);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_ms(nng_pipe pipe, Utf8String opt, out nng_duration val);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_ptr(nng_pipe pipe, Utf8String opt, out void* val);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_string(nng_pipe pipe, Utf8String opt, out Utf8String val);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_size(nng_pipe pipe, Utf8String opt, out nuint val);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_pipe_get_uint64(nng_pipe pipe, Utf8String opt, out ulong val);
  }
}
