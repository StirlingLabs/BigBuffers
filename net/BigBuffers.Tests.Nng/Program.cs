using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BigBuffers.Xpc.Async;
using NngNative;
using StirlingLabs.Utilities;

namespace BigBuffers.Tests.Nng
{
  using static LibNng;

  public static class Program
  {
    private static int _lastIssuedFreeEphemeralTcpPort = -1;
    private static int GetFreeEphemeralTcpPort()
    {
      bool IsFree(int realPort)
      {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        IPEndPoint[] listeners = properties.GetActiveTcpListeners();
        int[] openPorts = listeners.Select(item => item.Port).ToArray<int>();
        return openPorts.All(openPort => openPort != realPort);
      }

      const int ephemeralRangeSize = 16384;
      const int ephemeralRangeStart = 49152;

      var port = (_lastIssuedFreeEphemeralTcpPort + 1) % ephemeralRangeSize;

      while (!IsFree(ephemeralRangeStart + port))
        port = (port + 1) % ephemeralRangeSize;

      _lastIssuedFreeEphemeralTcpPort = port;

      return ephemeralRangeStart + port;
    }

    public static readonly object _consoleLock = new object();

    public static async Task Main()
    {
      string url;
      //var url = "inproc://NngSanityCheck";

      /*
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        url = $"ipc://NngSanityCheck{Environment.ProcessId}";
      else
      {
        var dir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.nng";
        var path = $"{dir}/{Environment.ProcessId}";
        Directory.CreateDirectory(dir);
        url = $"ipc://{path}";
      }*/

      var strV = nng_version().ToString();
      var v = Version.Parse(strV);
      if (v.Major < 1 || v.Minor < 5)
        throw new NotSupportedException();

      Info($"Starting NNG Sanity Check {strV}");

      TaskScheduler.UnobservedTaskException += (_, args) => {
        lock (_consoleLock)
        {
          var aex = args.Exception;
          Error("=== Begin Unobserved Task Exception(s) === ");
          foreach (var ex in aex.InnerExceptions)
          {
            Error(ex.GetType().FullName);
            Error(ex.Message);
            Error(ex.StackTrace);
          }
          Error("=== End Unobserved Task Exception(s) === ");
          Environment.FailFast("Debug Mode");
        }
      };

      AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
        lock (_consoleLock)
        {
          Error("=== Begin Unhandled Exception(s) === ");
          if (args.ExceptionObject is Exception ex)
          {
            Error(ex.GetType().FullName);
            Error(ex.Message);
            Error(ex.StackTrace);
          }
          Error("=== End Unhandled Exception(s) === ");
          Environment.FailFast("Debug Mode");
        }
      };

      AppDomain.CurrentDomain.FirstChanceException += (sender, args) => {
        lock (_consoleLock)
        {
          Warn("=== Begin First Chance Exception(s) === ");
          var ex = args.Exception;
          {
            Warn(ex.GetType().FullName);
            Warn(ex.Message);
            Warn(ex.StackTrace);
          }
          Warn("=== End First Chance Exception(s) === ");
        }
        Environment.FailFast("Debug Mode");
      };

      var msgsToFree = new ConcurrentQueue<IntPtr>();
      var aiosToFree = new ConcurrentQueue<IntPtr>();

      var sync = new SemaphoreSlim(0, 1);

      for (var attempt = 0; attempt < 1000000; ++attempt)
      {

        url = "tcp://[::1]:" + GetFreeEphemeralTcpPort();
        Info($"Attempt #{attempt} {url}");
        Utf8String u8Url = url;
        try
        {
          var clientCount = 2;

          async Task ServerFunc()
          {
            var errno = 0;

            // server
            var serverDone = new AsyncCountdownEvent(clientCount);
            Trace($"Opening server socket");
            errno = nng_rep0_open(out var socket);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            errno = nng_socket_set_string(socket, NNG_OPT_SOCKNAME, "Server");
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            Trace($"Creating server listener");
            errno = nng_listener_create(out var listener, socket, u8Url);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            Trace($"Starting server listener");
            errno = nng_listener_start(listener, 0);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());

            for (var client = 0; client < clientCount; ++client)
            {
              var serverReceivedMessage = new SemaphoreSlim(0, 1);
              await Task.Factory.StartNew((Func<Task>)(async () => {
                Trace($"Signalling server start");
                sync.Release();

                unsafe
                {
                  Trace($"Opening server receive context");
                  errno = nng_ctx_open(out var ctx, socket);
                  if (errno != 0)
                    throw new(nng_strerror(errno).ToString());

                  Trace($"Allocating server receive AIO");
                  nng_aio* aio = null;
                  errno = nng_aio_alloc_async(out aio, () => {
                    // ReSharper disable AccessToModifiedClosure
                    Assert(aio != null);
                    errno = nng_aio_result(aio);
                    if (errno != 0)
                      throw new(nng_strerror(errno).ToString());
                    Trace($"Received server message");
                    var msg = nng_aio_get_msg(aio);
                    {
                      var len = nng_msg_len(msg);
                      Assert(len != 0);
                      var body = nng_msg_body(msg);
                      Assert(body != null);
                      var s = new Utf8String((sbyte*)body).Substring(0, len);
                      Assert("Hello" == s.ToString());
                      Assert(msg != null);
                      nng_msg_free(msg); // safe w/ tcp
                      //msgsToFree.Enqueue((IntPtr)msg);
                      msg = null;
                      Assert(aio != null);
                      nng_aio_free(aio); // safe w/ tcp
                      //aiosToFree.Enqueue((IntPtr)aio);
                      aio = null;
                      serverReceivedMessage.Release();
                    }
                    {
                      nng_aio* aio = null;
                      Trace($"Allocating server send AIO 1");
                      errno = nng_aio_alloc_async(out aio, () => {
                        Trace($"Server sent message 1");
                        errno = nng_aio_result(aio);
                        if (errno != 0)
                          throw new(nng_strerror(errno).ToString());

                        // make sure we don't send zero-length message
                        //var len = nng_msg_len(msg);
                        //Assert(len != 0);

                        Trace($"Server freeing AIO 1");
                        // do not free msg
                        Assert(msg != null);
                        //nng_msg_free(msg); // must be deferred
                        //msgsToFree.Enqueue((IntPtr)msg);
                        msg = null;
                        Assert(aio != null);
                        nng_aio_free(aio); // safe with tcp
                        //aiosToFree.Enqueue((IntPtr)aio);
                        aio = null;

                        {
                          nng_aio* aio = null;
                          Trace($"Allocating server send AIO 2");
                          errno = nng_aio_alloc_async(out aio, () => {
                            Trace($"Server sent message 2");
                            errno = nng_aio_result(aio);
                            if (errno != 0)
                              throw new(nng_strerror(errno).ToString());

                            // make sure we don't send zero-length message
                            //var len = nng_msg_len(msg);
                            //Assert(len != 0);

                            Trace($"Server freeing AIO 2 ");
                            // do not free msg
                            Assert(msg != null);
                            //nng_msg_free(msg); // must be deferred
                            //msgsToFree.Enqueue((IntPtr)msg);
                            msg = null;
                            Assert(aio != null);
                            nng_aio_free(aio); // safe with tcp
                            //aiosToFree.Enqueue((IntPtr)aio);
                            aio = null;

                            Trace($"Server finished with client");
                            serverDone.Signal();
                          });

                          //Console.WriteLine($"Opening server send context");
                          //errno = nng_ctx_open(out var sendCtx, socket);
                          //if (errno != 0)
                          //  throw new(nng_strerror(errno).ToString());

                          Assert(msg == null);

                          Trace($"Server allocating message 2");
                          errno = nng_msg_alloc(out msg);
                          if (errno != 0)
                            throw new(nng_strerror(errno).ToString());

                          var u8Howdy = SizedUtf8String.Create("Howdy 2");
                          Assert(u8Howdy.Length != 0);
                          errno = nng_msg_append(msg, u8Howdy);
                          if (errno != 0)
                            throw new(nng_strerror(errno).ToString());

                          // make sure we don't send zero-length message
                          var len = nng_msg_len(msg);
                          Assert(len != 0);

                          nng_aio_set_msg(aio, msg);

                          Trace($"Sending server message async 2");
                          nng_ctx_send(ctx, aio);
                        }

                      });

                      Assert(msg == null);

                      //Console.WriteLine($"Opening server send context");
                      //errno = nng_ctx_open(out var sendCtx, socket);
                      //if (errno != 0)
                      //  throw new(nng_strerror(errno).ToString());

                      Trace($"Server allocating message 1");
                      errno = nng_msg_alloc(out msg);
                      if (errno != 0)
                        throw new(nng_strerror(errno).ToString());

                      var u8Howdy = SizedUtf8String.Create("Howdy 1");
                      Assert(u8Howdy.Length != 0);
                      errno = nng_msg_append(msg, u8Howdy);
                      if (errno != 0)
                        throw new(nng_strerror(errno).ToString());

                      // make sure we don't send zero-length message
                      var len = nng_msg_len(msg);
                      Assert(len != 0);

                      nng_aio_set_msg(aio, msg);

                      Trace($"Sending server message async 1");
                      nng_ctx_send(ctx, aio);
                    }
                    // ReSharper restore AccessToModifiedClosure
                  });
                  Assert(aio != null);
                  if (errno != 0)
                    throw new(nng_strerror(errno).ToString());
                  Trace($"Receiving server message async");
                  nng_ctx_recv(ctx, aio);
                }
              }));
              await serverReceivedMessage.WaitAsync();
            }
            //Trace($"Server waiting for outstanding clients");
            await serverDone.WaitAsync();
            Trace($"Server is done with clients");
            errno = nng_listener_close(listener);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            Trace($"Server cleaned up listener");
            errno = nng_close(socket);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            Trace($"Server cleaned up socket");
          }

          async Task ClientFunc()
          {
            // client
            var clientDone = new SemaphoreSlim(0, 1);
            if (!await sync.WaitAsync(60000))
            {
              Fail("Client sync wait timeout");
              return;
            }

            // client
            Trace($"Opening client socket");
            var errno = nng_req0_open(out var socket);
            //var errno = nng_pair0_open(out var socket);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            errno = nng_socket_set_string(socket, NNG_OPT_SOCKNAME, "Client");
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            Trace($"Creating client dialer");
            errno = nng_dialer_create(out var dialer, socket, u8Url);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            Trace($"Starting client dialer");
            errno = nng_dialer_start(dialer, 0);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());

            unsafe
            {
              Trace($"Allocating client message");
              errno = nng_msg_alloc(out var msg);
              if (errno != 0)
                throw new(nng_strerror(errno).ToString());
              nng_aio* aio = null;
              Trace($"Creating client send context");
              errno = nng_ctx_open(out var ctx, socket);
              if (errno != 0)
                throw new(nng_strerror(errno).ToString());
              Trace($"Allocating client send AIO");
              errno = nng_aio_alloc_async(out aio, () => {
                Trace($"Client sent message");

                // ReSharper disable AccessToModifiedClosure
                Assert(aio != null);
                errno = nng_aio_result(aio);
                if (errno != 0)
                  throw new(nng_strerror(errno).ToString());

                // make sure we didn't send zero-length message
                //var len = nng_msg_len(msg);
                //Assert(len != 0);

                Trace($"Client freeing AIO");
                // do not free msg
                Assert(msg != null);
                //nng_msg_free(msg); // must be deferred
                //msgsToFree.Enqueue((IntPtr)msg);
                msg = null;
                Assert(aio != null);
                nng_aio_free(aio); // safe with tcp
                //aiosToFree.Enqueue((IntPtr)aio);
                aio = null;

                {
                  nng_aio* aio = null;
                  Trace($"Allocating client receive AIO 1");
                  errno = nng_aio_alloc_async(out aio, () => {
                    // ReSharper disable AccessToModifiedClosure
                    Assert(aio != null);
                    Trace($"Reading client AIO received message 1");
                    errno = nng_aio_result(aio);
                    if (errno != 0)
                      throw new(nng_strerror(errno).ToString());
                    var msg = nng_aio_get_msg(aio);
                    Assert(msg != null);
                    var len = nng_msg_len(msg);
                    Assert(len != 0);
                    var body = nng_msg_body(msg);
                    Assert(body != null);
                    var s = new Utf8String((sbyte*)body).Substring(0, len);
                    Assert("Howdy 1" == s.ToString());

                    Trace($"Client freeing received message and AIO 1");
                    Assert(msg != null);
                    nng_msg_free(msg); // safe with tcp
                    //msgsToFree.Enqueue((IntPtr)msg);
                    msg = null;
                    Assert(aio != null);
                    nng_aio_free(aio); // safe with tcp
                    //aiosToFree.Enqueue((IntPtr)aio);
                    aio = null;

                    {
                      nng_aio* aio = null;
                      Trace($"Allocating client receive AIO 2");
                      errno = nng_aio_alloc_async(out aio, () => {
                        // ReSharper disable AccessToModifiedClosure
                        Assert(aio != null);
                        Trace($"Reading client AIO received message 2");
                        errno = nng_aio_result(aio);
                        if (errno != 0)
                          throw new(nng_strerror(errno).ToString());
                        var msg = nng_aio_get_msg(aio);
                        Assert(msg != null);
                        var len = nng_msg_len(msg);
                        Assert(len != 0);
                        var body = nng_msg_body(msg);
                        Assert(body != null);
                        var s = new Utf8String((sbyte*)body).Substring(0, len);
                        Assert("Howdy 2" == s.ToString());

                        Trace($"Client freeing received message and AIO 2");
                        Assert(msg != null);
                        nng_msg_free(msg); // safe with tcp
                        //msgsToFree.Enqueue((IntPtr)msg);
                        msg = null;
                        Assert(aio != null);
                        nng_aio_free(aio); // safe with tcp
                        //aiosToFree.Enqueue((IntPtr)aio);
                        aio = null;

                        Trace($"Client signalling done");
                        clientDone.Release();
                      });

                      Trace($"Receiving client message async 2");
                      nng_ctx_recv(ctx, aio);
                    }
                  });

                  Trace($"Receiving client message async 1");
                  nng_ctx_recv(ctx, aio);
                }
                // ReSharper restore AccessToModifiedClosure
              });
              Assert(aio != null);
              var u8Hello = SizedUtf8String.Create("Hello");
              Assert(u8Hello.Length != 0);
              errno = nng_msg_append(msg, u8Hello);
              if (errno != 0)
                throw new(nng_strerror(errno).ToString());

              // make sure we don't send zero-length message
              var len = nng_msg_len(msg);
              Assert(len != 0);

              Trace($"Setting client send AIO message");
              nng_aio_set_msg(aio, msg);
              Trace($"Sending client message async");
              nng_ctx_send(ctx, aio);
            }
            await clientDone.WaitAsync();
            errno = nng_close(socket);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
          }

          Trace($"Beginning async server and client tasks");

          try
          {
            var tasks = new Task[clientCount + 1];
            for (var i = 0; i < clientCount; ++i)
              tasks[i] = Task.Run(ClientFunc);
            tasks[clientCount] = Task.Run(ServerFunc);
            await Task.WhenAll(tasks);
          }
          catch (Exception ex)
          {
            Error("=== Begin Main Exception(s) === ");
            {
              Error(ex.GetType().FullName);
              Error(ex.Message);
              Error(ex.StackTrace);
            }
            Error("=== End Main Exception(s) === ");
            Environment.FailFast("Debug Mode");
          }

          Trace($"Completed async server and client tasks");

          Trace($"Performing deferred actions");
          unsafe
          {
            Trace($"Performing deferred message freeing");
            foreach (var msgToFree in msgsToFree)
            {
              var msg = (nng_msg*)msgToFree;
              // ReSharper disable once ConditionIsAlwaysTrueOrFalse
              Assert(msg != null);
              Trace($"Freeing Msg 0x{(IntPtr)msg:X16}");
              nng_msg_free(msg);
            }
            msgsToFree.Clear();

            Trace($"Performing deferred AIO freeing");
            foreach (var aioToFree in aiosToFree)
            {
              var aio = (nng_aio*)aioToFree;
              // ReSharper disable once ConditionIsAlwaysTrueOrFalse
              Assert(aio != null);
              Trace($"Freeing AIO 0x{(IntPtr)aio:X16}");
              nng_aio_free(aio);
            }
            aiosToFree.Clear();
          }
        }
        finally
        {
          u8Url.Free();
        }
        Trace($"Finished performing deferred actions");
      }
    }

    public static void Assert(bool condition, string? message = null, [CallerFilePath] string? fileName = null!,
      [CallerLineNumber] int lineNum = 0)
    {
      if (condition) return;
      Error($"Assertion failed at {fileName}:{lineNum}");
      if (message != null) Error(message);
      Environment.FailFast($"Assertion failed at {fileName}:{lineNum}");
    }

    public static void Fail(string? message = null, [CallerFilePath] string? fileName = null!, [CallerLineNumber] int lineNum = 0)
    {
      Error($"Failed at {fileName}:{lineNum}");
      if (message != null) Error(message);
      Environment.FailFast($"Failed at {fileName}:{lineNum}");
    }

    public static long _started = Stopwatch.GetTimestamp();

    public static void Trace(string fmt, params object[] args)
    {
      lock (_consoleLock)
      {
        var elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - _started);
        var mtid = Thread.CurrentThread.ManagedThreadId;
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"T {elapsed:g} {mtid:X3} " + string.Format(fmt, args));
        Console.ResetColor();
      }
    }
    public static void Info(string fmt, params object[] args)
    {
      lock (_consoleLock)
      {
        var elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - _started);
        var mtid = Thread.CurrentThread.ManagedThreadId;
        Console.ResetColor();
        Console.WriteLine($"I {elapsed:g} {mtid:X3} " + string.Format(fmt, args));
        Console.ResetColor();
      }
    }
    public static void Warn(string fmt, params object[] args)
    {
      lock (_consoleLock)
      {
        var elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - _started);
        var mtid = Thread.CurrentThread.ManagedThreadId;
        Console.ResetColor();
        Console.BackgroundColor = ConsoleColor.DarkYellow;
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"W {elapsed:g} {mtid:X3} " + string.Format(fmt, args));
        Console.ResetColor();
      }
    }
    public static void Error(string fmt, params object[] args)
    {
      lock (_consoleLock)
      {
        var elapsed = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - _started);
        var mtid = Thread.CurrentThread.ManagedThreadId;
        Console.ResetColor();
        Console.BackgroundColor = ConsoleColor.Red;
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine($"E {elapsed:g} {mtid:X3} " + string.Format(fmt, args));
        Console.ResetColor();
      }
    }
  }
}
