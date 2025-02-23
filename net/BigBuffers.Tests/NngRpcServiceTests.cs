#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BigBuffers.Xpc;
using BigBuffers.Xpc.Nng;
using FluentAssertions;
using Generated;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.ObjectModel;
using nng;
using nng.Native;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Assertions;
using static NngNative.LibNng;
using nng_aio = NngNative.nng_aio;
using nng_msg = NngNative.nng_msg;

namespace BigBuffers.Tests
{
  [Timeout(300000)] // 5 minutes
  [NonParallelizable]
  [FixtureLifeCycle(LifeCycle.SingleInstance)]
  public class NngRpcServiceTests
  {
    private static IAPIFactory<INngMsg> _factory = null!;

    [OneTimeSetUp]
    public static void OneTimeSetUp()
    {

      var path = Path.GetDirectoryName(typeof(NngRpcServiceTests).Assembly.Location);
      var ctx = new NngLoadContext(path);
      _factory = NngLoadContext.Init(ctx);
    }

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
    public static IEnumerable<string> GetSanityCheckUrls()
    {
      yield return "inproc://NngSanityCheck";
      //yield return "tcp://127.0.0.1:" + GetFreeEphemeralTcpPort();
      //yield return "tls+tcp://127.0.0.1:" + GetFreeEphemeralTcpPort();
      //yield return "tcp://[::1]:" + GetFreeEphemeralTcpPort();
      //yield return "tls+tcp://[::1]:" + GetFreeEphemeralTcpPort();
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        yield return $"ipc://NngSanityCheck-{Environment.ProcessId}";
      else
      {
        var dir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.nng";
        var path = $"{dir}/{Environment.ProcessId}";
        Directory.CreateDirectory(dir);
        yield return $"ipc://{path}";
      }
      //yield return "ws://127.0.0.1:" + GetFreeEphemeralTcpPort();
      //yield return "wss://127.0.0.1:" + GetFreeEphemeralTcpPort();
      //yield return "ws://[::1]:" + GetFreeEphemeralTcpPort();
      //yield return "wss://[::1]:" + GetFreeEphemeralTcpPort();
    }

    [Theory]
    [NonParallelizable]
    [Order(0)]
    [Timeout(5000)]
    public async Task NngInProcPairSanityCheck([ValueSource(nameof(GetSanityCheckUrls))] string url, [Range(1, 100)] int run)
    {
      var sanityCheckBytes = Encoding.UTF8.GetBytes("Sanity check");

      var sync = new SemaphoreSlim(0, 1);

      await Task.WhenAll(
        Task.Run(async () => {
          // server
          using var pair = _factory.PairOpen().Unwrap();
          pair.Listen(url).Unwrap();
          sync.Release();

          using var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();
          var msg = _factory.CreateMessage();
          msg.Append(sanityCheckBytes);
          await asyncCtx.Send(msg);
        }),
        Task.Run(async () => {
          // client
          if (!await sync.WaitAsync(1000))
          {
            Assert.Fail("Client sync wait timeout");
            return;
          }

          for (;;)
          {
            using var pair = _factory.PairOpen().Unwrap();
            var result = pair.Dial(url);
            if (result.TryError(out var error))
            {
              Assert.Warn($"Client failed to connect: {error}");
              await Task.Delay(1);
              continue;
            }

            using var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();
            var msg = (await asyncCtx.Receive(default)).Unwrap();
            msg.AsSpan().SequenceEqual(sanityCheckBytes).Should().BeTrue();
            break;
          }
        })
      );
    }


    [Theory]
    [NonParallelizable]
    [Order(0)]
    [Timeout(5000)]
    public async Task NngNativeInProcPairSanityCheck([ValueSource(nameof(GetSanityCheckUrls))] string url, [Range(1, 100)] int run)
    {
      var sanityCheckBytes = Encoding.UTF8.GetBytes("Sanity check");

      var sync = new SemaphoreSlim(0, 1);

      var strV = nng_version().ToString();
      var v = Version.Parse(strV);
      if (v.Major < 1 || v.Minor < 5)
        throw new NotSupportedException();

      var u8Url = (Utf8String)url;

      try
      {
        async Task ServerFunc()
        {
          var errno = 0;

          // server
          errno = nng_pair0_open(out var socket);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
          //errno = nng_socket_set_string(socket, NNG_OPT_SOCKNAME,  "Server");
          //if (errno != 0)
          //  throw new(nng_strerror(errno).ToString());
          errno = nng_listener_create(out var listener, socket, u8Url);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
          errno = nng_listener_start(listener, 0);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
          sync.Release();

          var done = new SemaphoreSlim(0, 1);
          unsafe
          {
            errno = nng_ctx_open(out var recvCtx, socket);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());

            nng_aio* aio = null;
            errno = nng_aio_alloc_async(out aio, () => {
              // ReSharper disable AccessToModifiedClosure
              Debug.Assert(aio != null);
              {
                var msg = nng_aio_get_msg(aio);
                var len = nng_msg_len(msg);
                var body = nng_msg_body(msg);
                var s = new Utf8String((sbyte*)body).Substring(0, len);
                Assert.Equals("Hello", s.ToString());
                nng_msg_free(msg);
                nng_aio_free(aio);
              }
              {
                errno = nng_msg_alloc(out var msg);
                if (errno != 0)
                  throw new(nng_strerror(errno).ToString());
                errno = nng_aio_alloc_async(out aio, () => {
                  nng_msg_free(msg);
                  nng_aio_free(aio);
                  done.Release();
                });
                errno = nng_ctx_open(out var sendCtx, socket);
                if (errno != 0)
                  throw new(nng_strerror(errno).ToString());
                var reply = SizedUtf8String.Create("Howdy");
                errno = nng_msg_append(msg, (byte*)reply.Pointer, reply.Length);
                if (errno != 0)
                  throw new(nng_strerror(errno).ToString());
                nng_aio_set_msg(aio, msg);
                nng_ctx_send(sendCtx, aio);
              }
              // ReSharper restore AccessToModifiedClosure
            });
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            nng_ctx_recv(recvCtx, aio);
          }
          await done.WaitAsync();
          errno = nng_close(socket);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
        }

        async Task ClientFunc()
        {
          // client
          if (!await sync.WaitAsync(1000))
          {
            Assert.Fail("Client sync wait timeout");
            return;
          }

          // client
          var errno = nng_pair0_open(out var socket);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
          //errno = nng_socket_set_string(socket, NNG_OPT_SOCKNAME,  "Client");
          //if (errno != 0)
          //  throw new(nng_strerror(errno).ToString());
          errno = nng_dialer_create(out var dialer, socket, u8Url);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
          errno = nng_dialer_start(dialer, 0);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());

          var done = new SemaphoreSlim(0, 1);
          unsafe
          {
            errno = nng_msg_alloc(out var msg);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            nng_aio* aio = null;
            errno = nng_aio_alloc_async(out aio, () => {
              // ReSharper disable AccessToModifiedClosure
              Debug.Assert(aio != null);
              nng_msg_free(msg);
              nng_aio_free(aio);

              errno = nng_ctx_open(out var recvCtx, socket);
              if (errno != 0)
                throw new(nng_strerror(errno).ToString());

              {
                nng_aio* aio = null;
                errno = nng_aio_alloc_async(out aio, () => {
                  // ReSharper disable AccessToModifiedClosure
                  Debug.Assert(aio != null);
                  var msg = nng_aio_get_msg(aio);
                  var len = nng_msg_len(msg);
                  var body = nng_msg_body(msg);
                  var s = new Utf8String((sbyte*)body).Substring(0, len);
                  Assert.Equals("Howdy", s.ToString());
                  nng_msg_free(msg);
                  nng_aio_free(aio);
                  done.Release();
                });
                nng_ctx_recv(recvCtx, aio);
              }
              // ReSharper restore AccessToModifiedClosure
            });
            errno = nng_ctx_open(out var sendCtx, socket);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            var reply = SizedUtf8String.Create("Hello");
            errno = nng_msg_append(msg, (byte*)reply.Pointer, reply.Length);
            if (errno != 0)
              throw new(nng_strerror(errno).ToString());
            nng_aio_set_msg(aio, msg);
            nng_ctx_send(sendCtx, aio);
          }
          await done.WaitAsync();
          errno = nng_close(socket);
          if (errno != 0)
            throw new(nng_strerror(errno).ToString());
        }

        await Task.WhenAll(
          Task.Run(ServerFunc),
          Task.Run(ClientFunc)
        );
      }
      finally
      {
        u8Url.Free();
      }
    }
    [Test]
    [NonParallelizable]
    [Order(1)]
    public async Task GeneralOperations()
    {
      const string url = "inproc://RpcService";
      var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var completed = false;
      var logger = TestContext.Out;
      try
      {
        await Task.WhenAll(
          Task.Run(async () => {
            // server
            var pair = _factory.PairOpen().Unwrap();
            pair.Listen(url).Unwrap();

            //var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();

            var server = new RpcServiceNngServerImpl(pair, _factory, logger);

            await server.RunAsync(cts.Token);
          }, cts.Token),
          Task.Run(async () => {
            // client
            var pair = _factory.PairOpen().Unwrap();
            await RetryOnThrowOrFail(() => pair.Dial(url), cts.Token);

            //var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();

            var client = new RpcServiceNng.Client(pair, _factory, logger);

            var runner = client.RunAsync(cts.Token);

            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
              Message.AddBodyType(bb, MessageBody.TextMessage);
              var msg = Message.EndMessage(bb).Resolve(bb);
              TextMessage.StartTextMessage(bb);
              TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
              var textContent = TextMessage.EndTextMessage(bb);
              body.Fill(textContent);
              subject.Fill("Hello World");
              content.Fill("Succeed");

              //msg.BodyAsTextMessage().Content.Should().Be("Succeed");

              var result = await client.Send(msg, cts.Token);
              result.Code.Should().Be(StatusCode.Success);
            }

            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
              Message.AddBodyType(bb, MessageBody.TextMessage);
              var msg = Message.EndMessage(bb).Resolve(bb);
              TextMessage.StartTextMessage(bb);
              TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
              var textContent = TextMessage.EndTextMessage(bb);
              body.Fill(textContent);
              subject.Fill("Hello World");
              content.Fill("Fail");

              //msg.BodyAsTextMessage().Content.Should().Be("Fail");

              var result = await client.Send(msg, cts.Token);
              result.Code.Should().Be(StatusCode.Failure);
            }
            completed = true;
            cts.Cancel();

            await runner;
          }, cts.Token)
        );
      }
      catch (TaskCanceledException)
      {
        if (!completed) throw;
      }

      completed.Should().BeTrue();
    }


    [Theory]
    [NonParallelizable]
    [Order(2)]
    public async Task GeneralOperations2([Range(1, 100)] int run)
    {
      var logger = run > 3 ? null : TestContext.Out;

      static double TimeStamp()
        => SharedCounters.GetTimeSinceStarted().TotalSeconds;

      logger?.WriteLine(
        $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} execution started =================================");

      var ctsTest = new CancellationTokenSource(Debugger.IsAttached
        ? TimeSpan.FromMinutes(5)
        : TimeSpan.FromSeconds(15));

      var sw = Stopwatch.StartNew();

      var completed = false;

      /*
      ctsTest.Token.Register(() => {
        logger?.WriteLine(
          $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} cancelled ctsTest at {sw.Elapsed.TotalSeconds:F3}s");
      });*/

      /*var watchdog = new Thread(() => {
        try
        {
          Thread.Sleep(30000);
        }
        catch (ThreadInterruptedException)
        {
          return;
        }
        if (completed || ctsTest.IsCancellationRequested)
          return;
        if (!Debugger.IsAttached)
          Debugger.Launch();
        Debugger.Break();
      });
      watchdog.Start();*/

      async Task Execute()
      {
        logger?.WriteLine(
          $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} creating linked cts for client and server");

        const string url = "inproc://RpcService2";

        var ctsClient = CancellationTokenSource.CreateLinkedTokenSource(ctsTest.Token);
        /*ctsClient.Token.Register(() => {
          logger?.WriteLine(
            $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} cancelled ctsClient at {sw.Elapsed.TotalSeconds:F3}s");
        });*/

        var ctsServer = CancellationTokenSource.CreateLinkedTokenSource(ctsTest.Token);
        /*ctsServer.Token.Register(() => {
          logger?.WriteLine(
            $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} cancelled ctsServer at {sw.Elapsed.TotalSeconds:F3}s");
        });*/

        try
        {
          logger?.WriteLine(
            $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} waiting for client and server runners");

          await Task.WhenAll(
            Task.Run(async () => {
              // server
              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} started server at {sw.Elapsed.TotalSeconds:F3}s");
              using var pair = _factory.PairOpen().Unwrap();
              pair.Listen(url).Unwrap();

              //var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();

              var server = new RpcService2NngServerImpl(pair, _factory, logger);

              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} running the server");

              await server.RunAsync(ctsServer.Token);

              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} ended server at {sw.Elapsed.TotalSeconds:F3}s");
            }, ctsServer.Token),
            Task.Run(async () => {
              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} started client at {sw.Elapsed.TotalSeconds:F3}s");
              // client
              using var pair = _factory.PairOpen().Unwrap();
              // ReSharper disable once AccessToDisposedClosure
              await RetryOnThrowOrFail(() => pair.Dial(url), ctsClient.Token);

              //var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();

              var client = new RpcService2Nng.Client(pair, _factory, logger);

              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} running the client");

              var runner = client.RunAsync(ctsClient.Token);

              {
                IEnumerable<Message> CreateBasicMessages()
                {
                  {
                    var bb = new BigBufferBuilder();
                    Message.StartMessage(bb);
                    Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
                    Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
                    Message.AddBodyType(bb, MessageBody.TextMessage);
                    var msg = Message.EndMessage(bb).Resolve(bb);
                    TextMessage.StartTextMessage(bb);
                    TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
                    var textContent = TextMessage.EndTextMessage(bb);
                    body.Fill(textContent);
                    subject.Fill("Hello World");
                    content.Fill("Succeed");
                    logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} sending 1st msg");
                    yield return msg;
                  }

                  {
                    var bb = new BigBufferBuilder();
                    Message.StartMessage(bb);
                    Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
                    Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
                    Message.AddBodyType(bb, MessageBody.TextMessage);
                    var msg = Message.EndMessage(bb).Resolve(bb);
                    TextMessage.StartTextMessage(bb);
                    TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
                    var textContent = TextMessage.EndTextMessage(bb);
                    body.Fill(textContent);
                    subject.Fill("Hello World");
                    content.Fill("Fail");
                    logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} sending 2nd msg");
                    yield return msg;
                  }
                }

                IEnumerable<Message> CreateReceivableMessages()
                {
                  {
                    var bb = new BigBufferBuilder();
                    Message.StartMessage(bb);
                    Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
                    Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
                    Message.AddBodyType(bb, MessageBody.TextMessage);
                    var msg = Message.EndMessage(bb).Resolve(bb);
                    TextMessage.StartTextMessage(bb);
                    TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
                    var textContent = TextMessage.EndTextMessage(bb);
                    body.Fill(textContent);
                    subject.Fill("Hello World");
                    content.Fill("Hello World");
                    logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} sending 1st recv msg");
                    yield return msg;
                  }

                  {
                    var bb = new BigBufferBuilder();
                    Message.StartMessage(bb);
                    Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
                    Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
                    Message.AddBodyType(bb, MessageBody.TextMessage);
                    var msg = Message.EndMessage(bb).Resolve(bb);
                    TextMessage.StartTextMessage(bb);
                    TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
                    var textContent = TextMessage.EndTextMessage(bb);
                    body.Fill(textContent);
                    subject.Fill("RE: Hello World");
                    content.Fill("What's up?");
                    logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} sending 2nd recv msg");
                    yield return msg;
                  }
                }

                var messages = EnumerableChannelReader.Create(CreateBasicMessages());

                var statuses = new EnumerableChannelWriter<Status>();

                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} sending messages");

                {
                  var t = Stopwatch.StartNew();

                  await client.SendMany(messages, statuses, ctsClient.Token);

                  logger?.WriteLine(
                    $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} done sending messages after {t.Elapsed.TotalSeconds:F3}s");
                }

                {
                  var i = 0;
                  foreach (var status in statuses.AsEnumerable())
                  {
                    switch (i++)
                    {
                      case 0:
                        status.Code.Should().Be(StatusCode.Success);
                        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 1");
                        break;
                      case 1:
                        status.Code.Should().Be(StatusCode.Failure);
                        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 2");
                        break;
                      default:
                        throw new AssertionException("More than 2 status replies.");
                    }
                  }
                }

                messages = EnumerableChannelReader.Create(CreateBasicMessages());

                var oneStatusForAll = await client.SendManyAtomic(messages, ctsClient.Token);

                oneStatusForAll.Code.Should().Be(StatusCode.Failure);
                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 3");

                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} disposing statuses");
                statuses.Dispose();

                statuses = new EnumerableChannelWriter<Status>();
                var recvMsgs = EnumerableChannelReader.Create(CreateReceivableMessages());
                await client.SendMany(recvMsgs, statuses, ctsClient.Token);
                {
                  var i = 0;
                  foreach (var status in statuses.AsEnumerable())
                  {
                    switch (i++)
                    {
                      case 0:
                        status.Code.Should().Be(StatusCode.Success);
                        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 4");
                        break;
                      case 1:
                        status.Code.Should().Be(StatusCode.Success);
                        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 5");
                        break;
                      default:
                        throw new AssertionException("More than 2 status replies from 2nd SendMany.");
                    }
                  }
                }

                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} disposing statuses 2");
                statuses.Dispose();

                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} building Empty message");
                // TODO: ReceiveMany
                var bb = new BigBufferBuilder();
                Empty.StartEmpty(bb);
                var empty = Empty.EndEmpty(bb).Resolve(bb);
                var recvdMsgs = new EnumerableChannelWriter<Message>();
                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} starting ReceiveMany");
                await client.ReceiveMany(empty, recvdMsgs, ctsClient.Token);
                logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} finished ReceiveMany");
                {

                  var i = 0;
                  foreach (var msg in recvdMsgs.AsEnumerable())
                  {
                    switch (i++)
                    {
                      case 0:
                        msg.Subject.Should().Be("Hello World");
                        msg.BodyAsTextMessage().Content.Should().Be("Hello World");
                        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 4");
                        break;
                      case 1:
                        msg.Subject.Should().Be("RE: Hello World");
                        msg.BodyAsTextMessage().Content.Should().Be("What's up?");
                        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} verified got status 5");
                        break;
                      default:
                        throw new AssertionException("More than 2 message replies from ReceiveMany.");
                    }
                  }
                }

              }

              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} completed in {sw.Elapsed.TotalSeconds:F3}s, finalizing");
              completed = true;

              ctsClient.Cancel();
              ctsServer.Cancel();

              await runner;
              logger?.WriteLine(
                $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} ended client at {sw.Elapsed.TotalSeconds:F3}s");
            }, ctsClient.Token)
          );
        }
        catch (TaskCanceledException)
        {
          if (!completed) throw;
        }

        completed.Should().BeTrue();
        ctsTest.IsCancellationRequested.Should().BeFalse();
        ctsClient.IsCancellationRequested.Should().BeTrue();
        ctsServer.IsCancellationRequested.Should().BeTrue();
        logger?.WriteLine($"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} finalized in {sw.Elapsed.TotalSeconds:F3}s");
      }

      await Execute().ConfigureAwait(false);

      logger?.WriteLine(
        $"[{TimeStamp():F3}] {nameof(GeneralOperations2)} R{run} execution complete ================================");

      // if true, we timed out instead of succeeded
      ctsTest.IsCancellationRequested.Should().BeFalse();

      // check log for messages, nothing should be hanging on this cancellation token except final msg
      ctsTest.Cancel();

      /*watchdog.Interrupt();
      watchdog.Join();*/
    }

    private static Task<NngResult<Unit>> RetryOnThrowOrFail(Func<NngResult<Unit>> func, CancellationToken cancellationToken)
      => RetryOnThrowOrFail(func, TimeSpan.FromSeconds(1), cancellationToken);

    private static async Task<NngResult<Unit>> RetryOnThrowOrFail(Func<NngResult<Unit>> func, TimeSpan delaySeconds,
      CancellationToken cancellationToken)
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        var result = func();
        if (result.IsOk())
          return result;
        await Task.Delay(delaySeconds, cancellationToken);
      }

      cancellationToken.ThrowIfCancellationRequested();

      throw new NotImplementedException();
    }
  }
}
