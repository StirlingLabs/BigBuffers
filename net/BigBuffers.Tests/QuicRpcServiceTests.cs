#nullable enable
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BigBuffers.Xpc;
using BigBuffers.Xpc.Http;
using BigBuffers.Xpc.Quic;
using FluentAssertions;
using Generated;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Certificates.Generation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Tests
{
  [SingleThreaded]
  public class QuicRpcServiceTests
  {
    static QuicRpcServiceTests()
    {
      ProfileOptimization.SetProfileRoot(Environment.CurrentDirectory);
      ProfileOptimization.StartProfile(nameof(QuicRpcServiceTests));
    }

    private static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    private static int _lastIssuedFreeEphemeralTcpPort = -1;
    private static readonly Type? QuicConnectionAbortedExceptionType =
      Type.GetType(
        "System.Net.Quic.QuicConnectionAbortedException, System.Net.Quic, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
    private static readonly Type? QuicOperationAbortedExceptionType =
      Type.GetType(
        "System.Net.Quic.QuicOperationAbortedException, System.Net.Quic, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
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

    [Test]
    //[Timeout(15000)]
    [Theory]
    [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
    [NonParallelizable]
    public async Task CreateAndDispatch(
      [Range(0, 10)] int run,
      [Values(
#if NET6_0_OR_GREATER
        "3.0",
#endif
        "2.0",
        "1.1"
      )]
      string httpVersion
    )
    {

#if !NET6_0_OR_GREATER
      if (httpVersion == "3.0") Assert.Inconclusive(".NET 6 is required for HTTP/3 support.");
#endif

      var isDebug = Debugger.IsAttached;
      var logger = run <= 1 || isDebug
        ? new DebugTestContextWriter(TestContext.Out)
        : null; //TestContext.Out;

      TaskScheduler.UnobservedTaskException += (_, args) => {
        var aex = args.Exception;
        if (aex.InnerExceptions.Count == 1)
        {
          var ex = aex.InnerException;
          var exType = ex.GetType();
          if (exType.IsAssignableTo(QuicConnectionAbortedExceptionType)
            || exType.IsAssignableTo(QuicOperationAbortedExceptionType))
            return;
        }
        Debugger.Break();
      };

      var bigDelays = run <= 5;

      var testTimeoutMs = 5000;
      var cts =
        Debugger.IsAttached ? new() : new CancellationTokenSource(testTimeoutMs);

      TestContext.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: using HTTP/{httpVersion}");
      var ct = cts.Token;

      var env = run <= 1 ? Environments.Development : Environments.Production;

      TestContext.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: pretending to be {env} environment");

      var whb = WebHost.CreateDefaultBuilder();
      whb.UseEnvironment(env);
      whb.SuppressStatusMessages(true);
#if NET6_0_OR_GREATER
      whb.UseQuic();
#endif
      whb.UseKestrel();
      whb.ConfigureKestrel(k => {
        k.ListenAnyIP(0, listenOptions => {
#if NET6_0_OR_GREATER
          listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
#else
          listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
#endif
          listenOptions.UseHttps();
          listenOptions.KestrelServerOptions.AddServerHeader = false;
        });
        k.ConfigureHttpsDefaults(httpsOptions => {
          httpsOptions.SslProtocols = SslProtocols.Tls13;
        });
      });
      whb.ConfigureLogging(l => {
        l.ClearProviders();
        if (env == Environments.Development)
        {
          l.AddConsole();
          l.AddDebug();
        }
      });
      whb.UseShutdownTimeout(TimeSpan.FromMilliseconds(5));

      var svc = new QuicRpcServiceTestImpl("Test", ct, logger: logger);

      whb.Configure(ab => {
        ab.UseQuicRpcService(svc);
      });

      using var wh = whb.Build();

      try
      {
        var saf = wh.ServerFeatures.Get<IServerAddressesFeature>()!;

        saf.Should().NotBeNull();

        using (var started = new SemaphoreSlim(0, 1))
        {
          await Task.Factory.StartNew(async () => {
            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: starting app");
            await wh.StartAsync(ct);
            started.Release();
            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: running app");
            await wh.WaitForShutdownAsync(ct);
            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: app finished");
          }, ct);

          await started.WaitAsync(ct);
        }

        var uri = saf.Addresses.First(u => u.StartsWith("https:"));
        if (uri.StartsWith("https://[::]"))
          uri = "https://[::1]" + uri.Substring(12);
        else if (uri.StartsWith("https://0.0.0.0"))
          uri = "https://localhost" + uri.Substring(15);
        else if (uri.StartsWith("https://+"))
          uri = "https://localhost" + uri.Substring(9);

        logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: {uri}");

        // come on, really?
        if (httpVersion == "3.0")
          AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", true);

#if NET6_0_OR_GREATER
        var clientHttpVersion = httpVersion == "1.1" ? HttpVersion.Version11
          : httpVersion == "2.0" ? HttpVersion.Version20
          : httpVersion == "3.0" ? HttpVersion.Version30
          : throw new NotSupportedException();
#else
        var clientHttpVersion = httpVersion == "1.1" ? HttpVersion.Version11
          : httpVersion == "2.0" ? HttpVersion.Version20
          : throw new NotSupportedException();
#endif

        using (var handler = new SocketsHttpHandler
          { SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true } })
        using (var client = new HttpClient(handler)
        {
          BaseAddress = new(uri),
          DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
          DefaultRequestVersion = clientHttpVersion
        })
        {
          HttpRequestMessage req;
          {
            var bb = new BigBufferBuilder();
            Message.StartMessage(bb);
            Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
            Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
            Message.AddBodyType(bb, MessageBody.Empty);
            Message.EndMessage(bb);
            Empty.StartEmpty(bb);
            e.Fill(Empty.EndEmpty(bb));
            subj.Fill("Hello");

            req = new(HttpMethod.Post, "/Unary")
            {
              Headers = { { "Accept", "application/x-big-buffers" } },
              Content = new ByteArrayContent(bb.SizedByteArray())
                { Headers = { { "Content-Type", "application/x-big-buffers" } } },
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
              Version = clientHttpVersion
            };
            if (httpVersion == "1.1")
            {
#if HTTP_1_1_KEEP_ALIVE_WORKS
              //req.Headers.Add("Connection", "Keep-Alive");
              //req.Headers.Add("Keep-Alive", $"timeout={testTimeoutMs/1000}");
#else
              //req.Headers.Add("Connection", "close");
              //req.Headers.ConnectionClose.Should().BeTrue();
#endif
            }
          }

          logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sending message 1 {req.RequestUri}");
          var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
          logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sent message 1 {req.RequestUri}");

          response.EnsureSuccessStatusCode();

          response.StatusCode.Should().Be(HttpStatusCode.OK);

          using (var msgs = new HttpMessageContent(logger))
          {

            req = new(HttpMethod.Post, "/ClientStreaming")
            {
              Headers = { { "Accept", "application/x-big-buffers" } },
              Content = msgs,
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
              Version = clientHttpVersion
            };
            if (httpVersion == "1.1")
            {
              req.Headers.Add("Transfer-Encoding", "chunked");
              req.Headers.TransferEncodingChunked.Should().BeTrue();
            }
            /*req.Headers.Add("Connection", "close");
            req.Headers.ConnectionClose.Should().BeTrue();*/

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: queuing message 2.1 {req.RequestUri}");
            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
              Message.AddBodyType(bb, MessageBody.Empty);
              var reply = Message.EndMessage(bb).Resolve(bb);
              Empty.StartEmpty(bb);
              e.Fill(Empty.EndEmpty(bb));
              subj.Fill("Message 2.1");

              while (!await msgs.TryAddMessageAsync(reply, ct))
              {
                await Task.Delay(1);
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: retrying queuing message 2.1 {req.RequestUri}");
              }
            }

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: queuing message 2.2 {req.RequestUri}");
            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
              Message.AddBodyType(bb, MessageBody.Empty);
              var reply = Message.EndMessage(bb).Resolve(bb);
              Empty.StartEmpty(bb);
              e.Fill(Empty.EndEmpty(bb));
              subj.Fill("Message 2.2");

              while (!await msgs.TryAddMessageAsync(reply, ct))
              {
                await Task.Delay(1);
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: retrying queuing message 2.2 {req.RequestUri}");
              }
            }

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sending message 2 {req.RequestUri}");
            var sendTask = client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            await Task.Factory.StartNew(async () => {

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: delaying before sending message 2.3");

              if (bigDelays)
                await Task.Delay(1, ct);
              else
                await Task.Yield();

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sending message 2.3");
              {
                var bb = new BigBufferBuilder();
                Message.StartMessage(bb);
                Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
                Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
                Message.AddBodyType(bb, MessageBody.Empty);
                var reply = Message.EndMessage(bb).Resolve(bb);
                Empty.StartEmpty(bb);
                e.Fill(Empty.EndEmpty(bb));
                subj.Fill("Message 2.3");

                while (!await msgs.TryAddMessageAsync(reply, ct))
                {
                  await Task.Delay(1);
                  logger?.WriteLine(
                    $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: retrying queuing message 2.3 {req.RequestUri}");
                }
              }

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: delaying before sending message 2.4");

              if (bigDelays)
                await Task.Delay(1, ct);
              else
                await Task.Yield();

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sending message 2.4");
              {
                var bb = new BigBufferBuilder();
                Message.StartMessage(bb);
                Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
                Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
                Message.AddBodyType(bb, MessageBody.Empty);
                var reply = Message.EndMessage(bb).Resolve(bb);
                Empty.StartEmpty(bb);
                e.Fill(Empty.EndEmpty(bb));
                subj.Fill("Message 2.4");

                while (!await msgs.TryAddMessageAsync(reply, ct))
                {
                  await Task.Delay(1);
                  logger?.WriteLine(
                    $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: retrying queuing message 2.4 {req.RequestUri}");
                }
              }

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: delaying before notifying end of message 2");

              if (bigDelays)
                await Task.Delay(1, ct);
              else
                await Task.Yield();

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: notifying end of message 2 {req.RequestUri}");

              msgs.CompleteAdding();
            });

            response = await sendTask;

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sent message 2 {req.RequestUri}");

            response.EnsureSuccessStatusCode();

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            if (httpVersion[0] < '2')
              response.Headers.TransferEncodingChunked.Should().BeTrue();

            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
              Message.AddBodyType(bb, MessageBody.Empty);
              Message.EndMessage(bb);
              Empty.StartEmpty(bb);
              e.Fill(Empty.EndEmpty(bb));
              subj.Fill("Hello");

              req = new(HttpMethod.Post, "/ServerStreaming")
              {
                Headers = { { "Accept", "application/x-big-buffers" } },
                Content = new ByteArrayContent(bb.SizedByteArray())
                  { Headers = { { "Content-Type", "application/x-big-buffers" } } },
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Version = clientHttpVersion
              };

              if (httpVersion == "1.1")
              {
                req.Headers.Add("Transfer-Encoding", "chunked");
                req.Headers.TransferEncodingChunked.Should().BeTrue();
              }
              /*req.Headers.Add("Connection", "close");
              req.Headers.ConnectionClose.Should().BeTrue();*/
            }

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sending message 3 {req.RequestUri}");
            response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sent message 3 {req.RequestUri}");

            response.EnsureSuccessStatusCode();

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            if (httpVersion[0] < '2')
              response.Headers.TransferEncodingChunked.Should().BeTrue();

            //response.Content.Headers.ContentLength.Should().BeNull();

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: message 3 content length: {response.Content.Headers.ContentLength ?? -1}");

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: reading message 3 content {req.RequestUri}");

            await using (var rspStream = response.Content.ReadAsStream(ct))
            {
              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: got message 3 stream {req.RequestUri}");

              long ReadHeader()
              {
                Span<byte> headerBytes = stackalloc byte[16];
                var read = rspStream.Read(headerBytes);
                if (read <= 0) return -1;
                return MemoryMarshal.Read<long>(headerBytes);
              }

              var msgsReceived = 0;
              while (true)
              {
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: looking for message 3 reply part {msgsReceived + 1}");
                var msgLen = ReadHeader();
                if (msgLen <= 0)
                {
                  logger?.WriteLine(
                    $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: there is no message 3 reply part {msgsReceived + 1}");
                  break;
                }
                var msgLenInt = checked((int)msgLen);
                msgsReceived++;
                using var mem = MemoryPool<byte>.Shared.Rent(msgLenInt);
                var bodyMem = mem.Memory;
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: expecting message 3 reply part {msgsReceived} to be {msgLen} bytes");
                var totalRead = 0;
                do
                {
                  var read = await rspStream.ReadAsync(bodyMem.Slice(totalRead), ct);
                  totalRead += read;
                  await Task.Yield();
                } while (totalRead < msgLenInt);
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: received message 3 reply part {msgsReceived}, 0x{Convert.ToHexString(bodyMem.Span)}");

                var msg = new Message(0, new(bodyMem, 0));
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: subject: {msg.Subject}");

                msg.Subject.Should().StartWith("RE");
              }
              msgsReceived.Should().Be(2);
            }
          }
          using (var msgs = new HttpMessageContent(logger))
          {

            req = new(HttpMethod.Post, "/BidirectionalStreaming")
            {
              Headers = { { "Accept", "application/x-big-buffers" } },
              Content = msgs,
              VersionPolicy = HttpVersionPolicy.RequestVersionExact,
              Version = clientHttpVersion
            };

            if (httpVersion == "1.1")
            {
              req.Headers.Add("Transfer-Encoding", "chunked");
              req.Headers.TransferEncodingChunked.Should().BeTrue();
            }
            /*req.Headers.Add("Connection", "close");
            req.Headers.ConnectionClose.Should().BeTrue();*/

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: queuing message 4.1 {req.RequestUri}");
            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
              Message.AddBodyType(bb, MessageBody.Empty);
              var reply = Message.EndMessage(bb).Resolve(bb);
              Empty.StartEmpty(bb);
              e.Fill(Empty.EndEmpty(bb));
              subj.Fill("Message 4.1");

              while (!await msgs.TryAddMessageAsync(reply, ct))
              {
                await Task.Delay(1, ct);
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: retrying queuing message 4.1 {req.RequestUri}");
              }
            }

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: queuing message 4.2 {req.RequestUri}");
            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
              Message.AddBodyType(bb, MessageBody.Empty);
              var reply = Message.EndMessage(bb).Resolve(bb);
              Empty.StartEmpty(bb);
              e.Fill(Empty.EndEmpty(bb));
              subj.Fill("Message 4.2");

              while (!await msgs.TryAddMessageAsync(reply, ct))
              {
                await Task.Delay(1, ct);
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: retrying queuing message 4.2 {req.RequestUri}");
              }

              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: notifying end of message 4 {req.RequestUri}");

              msgs.CompleteAdding();
            }

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sending message 4 {req.RequestUri}");
            var sendTask = client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            response = await sendTask;

            response.EnsureSuccessStatusCode();

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: sent message 4 {req.RequestUri}");

            if (httpVersion[0] < '2')
              response.Headers.TransferEncodingChunked.Should().BeTrue();

            //response.Content.Headers.ContentLength.Should().BeNull();

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: message 4 content length: {response.Content.Headers.ContentLength ?? -1}");

            logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: reading message 4 content {req.RequestUri}");

            await using (var rspStream = response.Content.ReadAsStream(ct))
            {
              logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: got message 4 stream {req.RequestUri}");

              long ReadHeader()
              {
                Span<byte> headerBytes = stackalloc byte[16];
                var read = rspStream.Read(headerBytes);
                if (read <= 0) return -1;
                return MemoryMarshal.Read<long>(headerBytes);
              }

              var msgsReceived = 0;
              while (true)
              {
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: looking for message 4 reply part {msgsReceived + 1}");
                var msgLen = ReadHeader();
                if (msgLen <= 0)
                {
                  logger?.WriteLine(
                    $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: there is no message 4 reply part {msgsReceived + 1}");
                  break;
                }
                var msgLenInt = checked((int)msgLen);
                msgsReceived++;
                using var mem = MemoryPool<byte>.Shared.Rent(msgLenInt);
                var bodyMem = mem.Memory;
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: expecting message 4 reply part {msgsReceived} to be {msgLen} bytes");
                var totalRead = 0;
                do
                {
                  var read = await rspStream.ReadAsync(bodyMem.Slice(totalRead), ct);
                  totalRead += read;
                  await Task.Yield();
                } while (totalRead < msgLenInt);
                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: received message 4 reply part {msgsReceived}, 0x{Convert.ToHexString(bodyMem.Span)}");

                var msg = new Message(0, new(bodyMem, 0));

                logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: subject: {msg.Subject}");

                msg.Subject.Should().StartWith("RE: Message 4");

              }
              msgsReceived.Should().Be(2);
            }
          }
        }

        logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: stopping app");

        await wh.StopAsync(ct);
      }
      finally
      {
        try
        {
          await wh.WaitForShutdownAsync(ct);
        }
        catch (OperationCanceledException)
        {
          // timeout
        }
        cts.Cancel();
        logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: end of test");
      }
    }

    public sealed class QuicRpcServiceTestImpl : QuicRpcServiceServerBase
    {
      public QuicRpcServiceTestImpl(string name, QuicListener listener,
        TextWriter? logger = null)
        : base(name, listener, logger) { }

      private enum Method : long
      {
        Unary = 1,
        ClientStreaming = 2,
        ServerStreaming = 3,
        BidirectionalStreaming = 4
      }

      protected override RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method)
      {
        if (method is not Method m)
          throw new InvalidOperationException();

        return m switch
        {
          Method.Unary => RpcMethodType.Unary,
          Method.ClientStreaming => RpcMethodType.ClientStreaming,
          Method.ServerStreaming => RpcMethodType.ServerStreaming,
          Method.BidirectionalStreaming => RpcMethodType.BidirectionalStreaming,
          _ => throw new ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override async Task<IMessage?> DispatchUnary<TMethodEnum>(
        TMethodEnum method,
        long sourceMsgId,
        ByteBuffer sourceByteBuffer,
        CancellationToken cancellationToken
      )
      {
        if (method is not Method m) throw new System.InvalidOperationException();
        return m switch
        {
          Method.Unary => await Reply(Unary(new(0, sourceByteBuffer), cancellationToken)),
          _ => throw new System.ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override async Task<IMessage?> DispatchClientStreaming<TMethodEnum>(
        TMethodEnum method,
        long sourceMsgId,
        AsyncProducerConsumerCollection<IMessage> reader,
        CancellationToken cancellationToken
      )
      {
        if (method is not Method m) throw new System.InvalidOperationException();
        return m switch
        {
          Method.ClientStreaming => await Reply(ClientStreaming(WrapReader<@Generated.@Message>(reader), cancellationToken)),
          _ => throw new System.ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override Task DispatchServerStreaming<TMethodEnum>(
        TMethodEnum method,
        long sourceMsgId,
        ByteBuffer sourceByteBuffer,
        CancellationToken cancellationToken
      )
      {
        if (method is not Method m) throw new System.InvalidOperationException();
        var ads = new System.Collections.Generic.List<System.IAsyncDisposable>(1);

        async System.Threading.Tasks.Task CleanUpContinuation(System.Threading.Tasks.Task _)
        {
          foreach (var ad in ads) await ad.DisposeAsync();
        }

        System.Threading.Tasks.Task CleanUpAfter(System.Threading.Tasks.Task t) => t.ContinueWith(CleanUpContinuation);

        System.Threading.Channels.ChannelWriter<T> Writer<T>() where T : struct, BigBuffers.IBigBufferEntity
          => Track(new QuicMsgStreamWriter<T>(ctx, sourceMsgId, Logger), ads);

        return m switch
        {
          Method.ServerStreaming => CleanUpAfter(ServerStreaming(new(0, sourceByteBuffer), Writer<@Generated.@Message>(), cancellationToken)),
          _ => throw new System.ArgumentOutOfRangeException(nameof(method))
        };
      }

      protected override Task DispatchStreaming<TMethodEnum>(
        TMethodEnum method,
        HttpContext ctx,
        AsyncProducerConsumerCollection<ByteBuffer> reader,
        CancellationToken cancellationToken
      )
      {
        if (method is not Method m) throw new System.InvalidOperationException();
        var ads = new System.Collections.Generic.List<System.IAsyncDisposable>(1);

        async System.Threading.Tasks.Task CleanUpContinuation(System.Threading.Tasks.Task _)
        {
          foreach (var ad in ads) await ad.DisposeAsync();
        }

        System.Threading.Tasks.Task CleanUpAfter(System.Threading.Tasks.Task t) => t.ContinueWith(CleanUpContinuation);

        System.Threading.Channels.ChannelWriter<T> Writer<T>() where T : struct, IBigBufferEntity
          => Track(new BigBuffers.Xpc.Http.HttpMessageStreamWriter<T>(ctx, Logger), ads);

        return m switch
        {
          Method.BidirectionalStreaming => CleanUpAfter(BidirectionalStreaming(WrapReader<@Generated.@Message>(reader),
            Writer<@Generated.@Message>(), cancellationToken)),
          _ => throw new System.ArgumentOutOfRangeException(nameof(method))
        };
      }


      public async Task<Message> Unary(Message m, CancellationToken cancellationToken)
      {
        var bb = new BigBufferBuilder();
        Message.StartMessage(bb);
        Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
        Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
        Message.AddBodyType(bb, MessageBody.Empty);
        var reply = Message.EndMessage(bb).Resolve(bb);
        Empty.StartEmpty(bb);
        e.Fill(Empty.EndEmpty(bb));
        subj.Fill("RE: " + m.Subject);
        return reply;
      }

      public async Task<Message> ClientStreaming(ChannelReader<Message> msgs, CancellationToken cancellationToken)
      {
        var subjects = new List<string>();
        await foreach (var msg in msgs.AsConsumingAsyncEnumerable(cancellationToken))
          subjects.Add(msg.Subject);

        var bb = new BigBufferBuilder();
        Message.StartMessage(bb);
        Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
        Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
        Message.AddBodyType(bb, MessageBody.Empty);
        var reply = Message.EndMessage(bb).Resolve(bb);
        Empty.StartEmpty(bb);
        e.Fill(Empty.EndEmpty(bb));
        subj.Fill($"RE: {string.Join(", ", subjects)}");
        return reply;
      }

      public async Task ServerStreaming(Message m, ChannelWriter<Message> writer, CancellationToken ct)
      {
        try
        {
          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming started");

          var subject = m.Subject;

          {
            var bb = new BigBufferBuilder();
            Message.StartMessage(bb);
            Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
            Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
            Message.AddBodyType(bb, MessageBody.Empty);
            var reply = Message.EndMessage(bb).Resolve(bb);
            Empty.StartEmpty(bb);
            e.Fill(Empty.EndEmpty(bb));
            subj.Fill($"RE: {subject}");

            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming delaying a bit");
            await Task.Delay(1, ct);

            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming writing reply 1");
            await writer.WriteAsync(reply, ct);
          }

          {
            var bb = new BigBufferBuilder();
            Message.StartMessage(bb);
            Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
            Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
            Message.AddBodyType(bb, MessageBody.Empty);
            var reply = Message.EndMessage(bb).Resolve(bb);
            Empty.StartEmpty(bb);
            e.Fill(Empty.EndEmpty(bb));
            subj.Fill($"RE Ctd.: {subject}");

            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming delaying a bit");
            await Task.Delay(1, ct);

            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming writing reply 2");
            await writer.WriteAsync(reply, ct);
          }

          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming finished");
        }
        finally
        {
          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming completing");
          writer.Complete();
          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: ServerStreaming completed");
        }
      }

      public async Task BidirectionalStreaming(ChannelReader<Message> msgs, ChannelWriter<Message> writer, CancellationToken ct)
      {
        try
        {
          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming started");

          await foreach (var msg in msgs.AsConsumingAsyncEnumerable(ct))
          {
            var subject = msg.Subject;
            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming got {subject}");

            var bb = new BigBufferBuilder();
            Message.StartMessage(bb);
            Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
            Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
            Message.AddBodyType(bb, MessageBody.Empty);
            var reply = Message.EndMessage(bb).Resolve(bb);
            Empty.StartEmpty(bb);
            e.Fill(Empty.EndEmpty(bb));
            subj.Fill($"RE: {subject}");

            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming delaying a bit");
            await Task.Delay(1, ct);

            await writer.WriteAsync(reply, ct);
            Logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming wrote RE: {subject}");
          }

          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming finished");
        }
        finally
        {
          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming completing");
          writer.Complete();
          Logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: BidirectionalStreaming completed");
        }
      }
    }
  }
}
