using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NngNative
{

  public static unsafe partial class LibNng
  {
#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_send_aio(nng_socket socket, nng_aio* aio);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_recv_aio(nng_socket socket, nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_aio_alloc(out nng_aio* aio, AioCallback callback, IntPtr arg);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
#if NET6_0_OR_GREATER
    public static extern int nng_aio_alloc(out nng_aio* aio, delegate* unmanaged[Cdecl, SuppressGCTransition]<IntPtr, void> callback,
      IntPtr arg);
#else
    public static extern int nng_aio_alloc(out nng_aio* aio, delegate* unmanaged[Cdecl]<IntPtr, void> callback, IntPtr arg);
#endif

#if NET5_0_OR_GREATER
    public static int nng_aio_alloc_async(out nng_aio* aio, Func<Task> callback)
      => nng_aio_alloc(out aio, &AsyncAioDispatchCallback, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
    public static int nng_aio_alloc_async(out nng_aio* aio, Action callback)
      => nng_aio_alloc(out aio, &AsyncAioDispatchCallback, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));

    public static int nng_aio_alloc(out nng_aio* aio, Action callback)
      => nng_aio_alloc(out aio, &SyncAioDispatchCallback, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
#else
    public static int nng_aio_alloc_async(out nng_aio* aio, Func<Task> callback)
      => nng_aio_alloc(out aio, AsyncAioDispatchCallbackDelegate, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
    public static int nng_aio_alloc_async(out nng_aio* aio, Action callback)
      => nng_aio_alloc(out aio, AsyncAioDispatchCallbackDelegate, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));

    public static int nng_aio_alloc(out nng_aio* aio, Action callback)
      => nng_aio_alloc(out aio, SyncAioDispatchCallbackDelegate, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
#endif

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_free(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_stop(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_aio_result(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint nng_aio_count(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_cancel(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_abort(nng_aio* aio, NngError error);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_wait(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_set_msg(nng_aio* aio, nng_msg* message);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nng_msg* nng_aio_get_msg(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_aio_set_input(nng_aio* aio, uint index, IntPtr param);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr nng_aio_get_input(nng_aio* aio, uint index);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int nng_aio_set_output(nng_aio* aio, uint index, IntPtr param);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void* nng_aio_get_output(nng_aio* aio, uint index);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_set_timeout(nng_aio* aio, nng_duration timeout);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    static extern int nng_aio_set_iov(nng_aio* aio, uint niov, nng_iov* iov);

    public static int nng_aio_set_iov(nng_aio* aio, ReadOnlySpan<nng_iov> iov)
    {
      if (iov.Length > 8) throw new ArgumentOutOfRangeException(nameof(iov), "Maximum of 8");
      fixed (nng_iov* pIov = iov)
        return nng_aio_set_iov(aio, (uint)iov.Length, pIov);
    }

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool nng_aio_begin(nng_aio* aio);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_finish(nng_aio* aio, NngError error);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void nng_aio_defer(nng_aio* aio, AioCancelCallback callback, IntPtr data);

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
#if NET6_0_OR_GREATER
    public static extern void nng_aio_defer(nng_aio* aio,
      delegate* unmanaged[Cdecl, SuppressGCTransition]<nng_aio*, IntPtr, NngError, void> function,
      IntPtr data);
#else
    public static extern void nng_aio_defer(nng_aio* aio, delegate* unmanaged[Cdecl]<nng_aio*, IntPtr, NngError, void> function, IntPtr data);
#endif

#if NET5_0_OR_GREATER
    public static void nng_aio_defer_async(nng_aio* aio, Func<Task, NngError> callback)
      => nng_aio_defer(aio, &AsyncAioCancelDispatchCallback, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
    public static void nng_aio_defer_async(nng_aio* aio, Action<NngError> callback)
      => nng_aio_defer(aio, &AsyncAioCancelDispatchCallback, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
    public static void nng_aio_defer(nng_aio* aio, Action<NngError> callback)
      => nng_aio_defer(aio, &SyncAioCancelDispatchCallback, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
#else
    public static void nng_aio_defer_async(nng_aio* aio, Func<Task, NngError> callback)
      => nng_aio_defer(aio, AsyncAioCancelDispatchCallbackDelegate, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
    public static void nng_aio_defer_async(nng_aio* aio, Action<NngError> callback)
      => nng_aio_defer(aio, AsyncAioCancelDispatchCallbackDelegate, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
    public static void nng_aio_defer(nng_aio* aio, Action<NngError> callback)
      => nng_aio_defer(aio, AsyncAioCancelDispatchCallbackDelegate, GCHandle.ToIntPtr(GCHandle.Alloc(callback)));
#endif

#if NET5_0_OR_GREATER
    [SuppressGCTransition]
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "nng_sleep_aio")]
    private static extern void nng_sleep_aio_short(nng_duration duration, nng_aio* aio);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "nng_sleep_aio")]
    private static extern void nng_sleep_aio_long(nng_duration duration, nng_aio* aio);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void nng_sleep_aio(nng_duration duration, nng_aio* aio)
    {
      if (duration.TimeMs <= 10)
        nng_sleep_aio_short(duration, aio);
      else
        nng_sleep_aio_long(duration, aio);

    }
  }


  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public delegate void AioCallback(IntPtr arg);

  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
  public unsafe delegate void AioCancelCallback(nng_aio* aio, IntPtr arg, NngError err);

  public static partial class LibNng
  {
#if !NET5_0_OR_GREATER
    private static readonly AioCallback AsyncAioDispatchCallbackDelegate = AsyncAioDispatchCallback;
    private static readonly AioCallback SyncAioDispatchCallbackDelegate = SyncAioDispatchCallback;
    private static readonly unsafe AioCancelCallback AsyncAioCancelDispatchCallbackDelegate = AsyncAioCancelDispatchCallback;
    private static readonly unsafe AioCancelCallback SyncAioCancelDispatchCallbackDelegate = SyncAioCancelDispatchCallback;
#endif

    public static event Action<Exception> UnhandledAioCallbackException;
    public static event Action<Exception> UnhandledAioCancelCallbackException;

#if NET6_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl), typeof(CallConvSuppressGCTransition) })]
#elif NET5_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static void AsyncAioDispatchCallback(IntPtr arg)
    {
      GCHandle h = default;
      try
      {
        h = GCHandle.FromIntPtr(arg);
        var t = h.Target;
        switch (t)
        {
          case Func<Task> f:
            Task.Run(f!);
            break;
          case Action a:
            Task.Run(a!);
            break;
          default:
            throw new NotImplementedException();
        }
      }
      catch (Exception ex)
      {
        if (Debugger.IsAttached) Debugger.Break();
        UnhandledAioCallbackException?.Invoke(ex);
      }
      finally
      {
        h.Free();
      }
    }


#if NET6_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl), typeof(CallConvSuppressGCTransition) })]
#elif NET5_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static void SyncAioDispatchCallback(IntPtr arg)
    {
      GCHandle h = default;
      try
      {
        h = GCHandle.FromIntPtr(arg);
        var action = (Action)h.Target;
        action!();
      }
      catch (Exception ex)
      {
        if (Debugger.IsAttached) Debugger.Break();
        UnhandledAioCallbackException?.Invoke(ex);
      }
      finally
      {
        h.Free();
      }
    }


#if NET6_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl), typeof(CallConvSuppressGCTransition) })]
#elif NET5_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe void AsyncAioCancelDispatchCallback(nng_aio* aio, IntPtr arg, NngError error)
      => AsyncAioCancelDispatchCallbackInternal(arg, error);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AsyncAioCancelDispatchCallbackInternal(IntPtr arg, NngError error)
    {
      GCHandle h = default;
      try
      {
        h = GCHandle.FromIntPtr(arg);
        var t = h.Target;
        switch (t)
        {
          case Func<NngError, Task> f:
            Task.Factory.StartNew(async o => {
                var s = ((Func<NngError, Task> f, NngError error))o;
                await s.f(s.error);
              }, (f, error))
              .ConfigureAwait(false)
              .GetAwaiter()
              .GetResult();
            break;
          case Action<NngError> a:
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
            ThreadPool.QueueUserWorkItem(a, error, false);
#else
            Task.Factory.StartNew(async o => {
                var s = ((Action<NngError> a, NngError error))o;
                s.a(s.error);
              }, (a, error))
              .ConfigureAwait(false)
              .GetAwaiter()
              .GetResult();
#endif
            break;
          default:
            throw new NotImplementedException();
        }
      }
      catch (Exception ex)
      {
        if (Debugger.IsAttached) Debugger.Break();
        UnhandledAioCancelCallbackException?.Invoke(ex);
      }
      finally
      {
        h.Free();
      }
    }


#if NET6_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl), typeof(CallConvSuppressGCTransition) })]
#elif NET5_0_OR_GREATER
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe void SyncAioCancelDispatchCallback(nng_aio* aio, IntPtr arg, NngError error)
      => SyncAioCancelDispatchCallbackInternal(arg, error);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SyncAioCancelDispatchCallbackInternal(IntPtr arg, NngError error)
    {
      GCHandle h = default;
      try
      {
        h = GCHandle.FromIntPtr(arg);
        var action = (Action<NngError>)h.Target;
        action!(error);
      }
      catch (Exception ex)
      {
        if (Debugger.IsAttached) Debugger.Break();
        UnhandledAioCancelCallbackException?.Invoke(ex);
      }
      finally
      {
        h.Free();
      }
    }
  }
}
