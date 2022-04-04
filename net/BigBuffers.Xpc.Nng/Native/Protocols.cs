using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NngNative{

    public static unsafe partial class LibNng
    {
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_bus0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_bus0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pair0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pair0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pair1_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pair1_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pub0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pub0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pull0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_pull0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_push0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_push0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_rep0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_rep0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_req0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_req0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_respondent0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_respondent0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_sub0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_sub0_open_raw(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_surveyor0_open(out nng_socket socket);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int nng_surveyor0_open_raw(out nng_socket socket);
    }
}
