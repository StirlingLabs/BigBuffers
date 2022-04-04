using System;
using System.Collections.Generic;

namespace NngNative
{
  public static partial class LibNng
  {

    /// <summary>
    /// Maximum number of scatter/gather iov supported (see <a href=https://nng.nanomsg.org/man/v1.3.2/nng_aio_set_iov.3.html>nng_aio_set_iov</a>)
    /// </summary>
    public const int MAX_IOV_BUFFERS = 8;

    public const int NNG_MAXADDRLEN = 128;

    public const int NNG_DURATION_INFINITE = -1;
    public const int NNG_DURATION_DEFAULT = -2;
    public const int NNG_DURATION_ZERO = 0;

    public const string NNG_OPT_SOCKNAME = "socket-name";
    public const string NNG_OPT_RAW = "raw";
    public const string NNG_OPT_PROTO = "protocol";
    public const string NNG_OPT_PROTONAME = "protocol-name";
    public const string NNG_OPT_PEER = "peer";
    public const string NNG_OPT_PEERNAME = "peer-name";
    public const string NNG_OPT_RECVBUF = "recv-buffer";
    public const string NNG_OPT_SENDBUF = "send-buffer";
    public const string NNG_OPT_RECVFD = "recv-fd";
    public const string NNG_OPT_SENDFD = "send-fd";
    public const string NNG_OPT_RECVTIMEO = "recv-timeout";
    public const string NNG_OPT_SENDTIMEO = "send-timeout";
    public const string NNG_OPT_LOCADDR = "local-address";
    public const string NNG_OPT_REMADDR = "remote-address";
    public const string NNG_OPT_URL = "url";
    public const string NNG_OPT_MAXTTL = "ttl-max";
    public const string NNG_OPT_RECVMAXSZ = "recv-size-max";
    public const string NNG_OPT_RECONNMINT = "reconnect-time-min";
    public const string NNG_OPT_RECONNMAXT = "reconnect-time-max";

    public const string NNG_OPT_TLS_CONFIG = "tls-config";
    public const string NNG_OPT_TLS_AUTH_MODE = "tls-authmode";
    public const string NNG_OPT_TLS_CERT_KEY_FILE = "tls-cert-key-file";
    public const string NNG_OPT_TLS_CA_FILE = "tls-ca-file";
    public const string NNG_OPT_TLS_SERVER_NAME = "tls-server-name";
    public const string NNG_OPT_TLS_VERIFIED = "tls-verified";
    public const string NNG_OPT_TCP_NODELAY = "tcp-nodelay";
    public const string NNG_OPT_TCP_KEEPALIVE = "tcp-keepalive";
    public const string NNG_OPT_TCP_BOUND_PORT = "tcp-bound-port";

    public const string NNG_OPT_IPC_SECURITY_DESCRIPTOR = "ipc:security-descriptor";
    public const string NNG_OPT_IPC_PERMISSIONS = "ipc:permissions";
    public const string NNG_OPT_IPC_PEER_UID = "ipc:peer-uid";
    public const string NNG_OPT_IPC_PEER_GID = "ipc:peer-gid";
    public const string NNG_OPT_IPC_PEER_PID = "ipc:peer-pid";
    public const string NNG_OPT_IPC_PEER_ZONEID = "ipc:peer-zoneid";

    public const string NNG_OPT_WS_REQUEST_HEADERS = "ws:request-headers";
    public const string NNG_OPT_WS_RESPONSE_HEADERS = "ws:response-headers";
    public const string NNG_OPT_WS_RESPONSE_HEADER = "ws:response-header:";
    public const string NNG_OPT_WS_REQUEST_HEADER = "ws:request-header:";
    public const string NNG_OPT_WS_REQUEST_URI = "ws:request-uri";
    public const string NNG_OPT_WS_SENDMAXFRAME = "ws:txframe-max";
    public const string NNG_OPT_WS_RECVMAXFRAME = "ws:rxframe-max";
    public const string NNG_OPT_WS_PROTOCOL = "ws:protocol";

    public const string NNG_OPT_REQ_RESENDTIME = "req:resend-time";

    public const string NNG_OPT_SUB_SUBSCRIBE = "sub:subscribe";
    public const string NNG_OPT_SUB_UNSUBSCRIBE = "sub:unsubscribe";
    public const string NNG_OPT_SUB_PREFNEW = "sub:prefnew";

    [Obsolete("pair v1 polyamorous mode is deprecated in NNG v1.3.0")]
    public const string NNG_OPT_PAIR1_POLY = "pair1:polyamorous";

    public const string NNG_OPT_SURVEYOR_SURVEYTIME = "surveyor:survey-time";

    public const string NNG_OPT_WSS_REQUEST_HEADERS = NNG_OPT_WS_REQUEST_HEADERS;
    public const string NNG_OPT_WSS_RESPONSE_HEADERS = NNG_OPT_WS_RESPONSE_HEADERS;

    public const int NNG_OK = 0;
    public const int NNG_EINTR = 1;
    public const int NNG_ENOMEM = 2;
    public const int NNG_EINVAL = 3;
    public const int NNG_EBUSY = 4;
    public const int NNG_ETIMEDOUT = 5;
    public const int NNG_ECONNREFUSED = 6;
    public const int NNG_ECLOSED = 7;
    public const int NNG_EAGAIN = 8;
    public const int NNG_ENOTSUP = 9;
    public const int NNG_EADDRINUSE = 10;
    public const int NNG_ESTATE = 11;
    public const int NNG_ENOENT = 12;
    public const int NNG_EPROTO = 13;
    public const int NNG_EUNREACHABLE = 14;
    public const int NNG_EADDRINVAL = 15;
    public const int NNG_EPERM = 16;
    public const int NNG_EMSGSIZE = 17;
    public const int NNG_ECONNABORTED = 18;
    public const int NNG_ECONNRESET = 19;
    public const int NNG_ECANCELED = 20;
    public const int NNG_ENOFILES = 21;
    public const int NNG_ENOSPC = 22;
    public const int NNG_EEXIST = 23;
    public const int NNG_EREADONLY = 24;
    public const int NNG_EWRITEONLY = 25;
    public const int NNG_ECRYPTO = 26;
    public const int NNG_EPEERAUTH = 27;
    public const int NNG_ENOARG = 28;
    public const int NNG_EAMBIGUOUS = 29;
    public const int NNG_EBADTYPE = 30;
    public const int NNG_ECONNSHUT = 31;
    public const int NNG_EINTERNAL = 1000;
    public const int NNG_ESYSERR = 0x10000000;
    public const int NNG_ETRANERR = 0x20000000;

    public const int NNG_PIPE_EV_ADD_PRE = 0;
    public const int NNG_PIPE_EV_ADD_POST = 1;
    public const int NNG_PIPE_EV_REM_POST = 2;
  }
}
