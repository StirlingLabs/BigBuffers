using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace NngNative
{

  public static unsafe partial class LibNng
  {
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_stats_get(out nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_stats_free(nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern unsafe Utf8String nng_stat_name(nng_stat* statsp);

    /// <summary>
    /// Makes managed copy of unmanaged string so nng_stats_free() (which frees the strings) can be called without issue.
    /// </summary>
    public static string nng_stat_name_string(nng_stat* statsp)
      => nng_stat_name(statsp).ToString();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern unsafe Utf8String nng_stat_desc(nng_stat* statsp);

    /// <summary>
    /// Makes managed copy of unmanaged string so nng_stats_free() (which frees the strings) can be called without issue.
    /// </summary>
    public static string nng_stat_desc_string(nng_stat* statsp)
      => nng_stat_desc(statsp).ToString();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_stat_type_enum nng_stat_type(nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong nng_stat_value(nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_unit_enum nng_stat_unit(nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern unsafe Utf8String nng_stat_string(nng_stat* statsp);

    /// <summary>
    /// Makes managed copy of unmanaged string so nng_stats_free() (which frees the strings) can be called without issue.
    /// </summary>
    public static string nng_stat_string_string(nng_stat* statsp)
      => nng_stat_string(statsp).ToString();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_stat* nng_stat_next(nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_stat* nng_stat_child(nng_stat* statsp);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong nng_stat_timestamp(nng_stat* statsp);
  }
}
