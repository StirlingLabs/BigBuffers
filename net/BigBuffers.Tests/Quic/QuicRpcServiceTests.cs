#nullable enable
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BigBuffers.Xpc;
using FluentAssertions;
using Generated;
using JetBrains.Annotations;
using JetBrains.Profiler.SelfApi;
using NUnit.Framework;
using NUnit.Framework.Internal;
using StirlingLabs.MsQuic;

namespace BigBuffers.Tests;

[SingleThreaded]
public class QuicRpcServiceTests {

  private const bool UseProfiling = false;

  static QuicRpcServiceTests() {
    ProfileOptimization.SetProfileRoot(Environment.CurrentDirectory);
    ProfileOptimization.StartProfile(nameof(QuicRpcServiceTests));
  }

  [OneTimeSetUp]
  public static void OneTimeSetUp() {
    if (UseProfiling) {
      DotTrace.EnsurePrerequisite();
      var config = new DotTrace.Config();
      var fn = $"{TestContext.CurrentContext.Test.ClassName}.dtt";
      var i = 1;
      while (File.Exists(fn))
        fn = $"{TestContext.CurrentContext.Test.ClassName}-{++i}.dtt";

      config.SaveToFile(fn, true);
      config.UseTimelineProfilingType(true);
      DotTrace.Attach(config);
    }
  }

  [SetUp]
  public static void SetUp() {
    if (UseProfiling) {
      DotTrace.StartCollectingData();
    }
  }

  [TearDown]
  public static void TearDown() {
    if (UseProfiling) {
      DotTrace.SaveData();
    }
  }

  [OneTimeTearDown]
  public static void OneTimeTearDown() {
    if (UseProfiling) {
      DotTrace.Detach();
    }
  }

  private static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

  private static int _lastIssuedFreeEphemeralTcpPort = -1;

  private static readonly Type? QuicConnectionAbortedExceptionType =
    Type.GetType(
      "System.Net.Quic.QuicConnectionAbortedException, System.Net.Quic, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

  private static readonly Type? QuicOperationAbortedExceptionType =
    Type.GetType(
      "System.Net.Quic.QuicOperationAbortedException, System.Net.Quic, Version=6.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

  private static readonly string AsmDir = Path.GetDirectoryName(new Uri(typeof(QuicRpcServiceTests).Assembly.Location).LocalPath)!;

  private static readonly string P12Path = Path.Combine(AsmDir, "localhost.p12");

  private static readonly QuicCertificate Cert = new QuicCertificate(policy => {
    policy.RevocationMode = X509RevocationMode.NoCheck;
    policy.DisableCertificateDownloads = false;
    policy.VerificationFlags |= X509VerificationFlags.AllowUnknownCertificateAuthority
      | X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown
      | X509VerificationFlags.IgnoreCtlSignerRevocationUnknown
      | X509VerificationFlags.IgnoreRootRevocationUnknown
      | X509VerificationFlags.IgnoreEndRevocationUnknown;
  }, File.OpenRead(P12Path));

  private static int GetFreeEphemeralTcpPort() {
    bool IsFree(int realPort) {
      var properties = IPGlobalProperties.GetIPGlobalProperties();
      var listeners = properties.GetActiveTcpListeners();
      var openPorts = listeners.Select(item => item.Port).ToArray();
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

  [MustUseReturnValue]
  public StringBuilder RecordExceptions(AggregateException aex, StringBuilder? sb = null) {
    if (sb is null) sb = new();
    sb.AppendLine(aex.GetType().AssemblyQualifiedName);
    sb.AppendLine(aex.Message);
    sb.AppendLine(aex.StackTrace);

    foreach (var iex in aex.InnerExceptions)
      sb = RecordExceptions(iex, sb);
    return sb;
  }

  [MustUseReturnValue]
  public StringBuilder RecordExceptions(Exception ex, StringBuilder? sb) {
    if (sb is null) sb = new();
    while (true) {
      if (ex is AggregateException aex) {
        sb = RecordExceptions(aex, sb);
        return sb;
      }

      sb.AppendLine(ex.GetType().AssemblyQualifiedName);
      sb.AppendLine(ex.Message);
      sb.AppendLine(ex.StackTrace);

      var iex = ex.InnerException;
      if (iex is not null) {
        ex = iex;
        continue;
      }

      break;
    }

    return sb;
  }

  // TODO: confirm client and server can identify when a stream or connection is unexpectedly interrupted or terminated

  //[Timeout(15000)]
  [Theory]
  [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
  [NonParallelizable]
  public async Task UnaryRoundTrip() {
    var logger = new DebugTestContextWriter(TestContext.Out);

    TaskScheduler.UnobservedTaskException += (_, args) => {
      var aex = args.Exception;
      if (aex.InnerExceptions.Count == 1) {
        var ex = aex.InnerException;
        var exType = ex.GetType();
        if (exType.IsAssignableTo(QuicConnectionAbortedExceptionType)
            || exType.IsAssignableTo(QuicOperationAbortedExceptionType))
          return;
      }

      var report = RecordExceptions(aex).ToString();
      Console.Error.WriteLine(report);
      System.Diagnostics.Debug.WriteLine(report);
      Assert.Fail(report);
    };

    //var bigDelays = run <= 5;

    var testTimeoutMs = 5000;
    var cts = new CancellationTokenSource(testTimeoutMs);

    var testName = TestContext.CurrentContext.Test.FullName;
    using var reg = new QuicRegistration(testName);
    using var listenerCfg = new QuicServerConfiguration(reg, "Test");
    using var listener = new QuicListener(listenerCfg);
    listenerCfg.ConfigureCredentials(Cert);

    var service = new RpcService3Impl();

    using var server = new RpcService3Quic.Server(service, listener, "Test", logger);

    var port = GetFreeEphemeralTcpPort();

    var ep = new IPEndPoint(IPAddress.IPv6Any, port);

    using var clientCfg = new QuicClientConfiguration(reg, "Test");
    clientCfg.ConfigureCredentials();

    using var clientConnection = new QuicClientConnection(clientCfg);

    listener.Start(ep);

    var sw = Stopwatch.StartNew();
    try {
      var client = new RpcService3Quic.Client(clientConnection,"Test", logger);

      await clientConnection.ConnectAsync("::1", (ushort)ep.Port);

      TestContext.WriteLine("Building message message to send");
      var bb = new BigBufferBuilder();
      Message.StartMessage(bb);
      Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
      Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
      Message.AddBodyType(bb, MessageBody.Empty);
      Message.EndMessage(bb);
      Empty.StartEmpty(bb);
      e.Fill(Empty.EndEmpty(bb));
      subj.Fill("Hello");

      TestContext.WriteLine("Sending hello message");

      var ct = cts.Token;

      var result = await client.Unary(new(0, bb.ByteBuffer), ct);

      TestContext.WriteLine("Received a message");

      result.Subject.Should().Be("RE: Hello");

      TestContext.WriteLine("Received \"Hello\" successfully");
    }
    finally {
      TestContext.WriteLine("Shutting down...");
      clientConnection.Close();
      listener.Stop();
      reg.Shutdown(0);
      TestContext.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");
    }

    TestContext.WriteLine("Disposing...");
  }

  //[Timeout(15000)]
  [Theory]
  [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
  [NonParallelizable]
  public async Task ClientStreamingRoundTrip() {
    var logger = new DebugTestContextWriter(TestContext.Out);

    TaskScheduler.UnobservedTaskException += (_, args) => {
      var aex = args.Exception;
      if (aex.InnerExceptions.Count == 1) {
        var ex = aex.InnerException;
        var exType = ex.GetType();
        if (exType.IsAssignableTo(QuicConnectionAbortedExceptionType)
            || exType.IsAssignableTo(QuicOperationAbortedExceptionType))
          return;
      }

      var report = RecordExceptions(aex).ToString();
      Console.Error.WriteLine(report);
      System.Diagnostics.Debug.WriteLine(report);
      Assert.Fail(report);
    };

    //var bigDelays = run <= 5;

    var testTimeoutMs = 5000;
    var cts = new CancellationTokenSource(testTimeoutMs);

    var testName = TestContext.CurrentContext.Test.FullName;
    using var reg = new QuicRegistration(testName);
    using var listenerCfg = new QuicServerConfiguration(reg, "Test");
    using var listener = new QuicListener(listenerCfg);
    listenerCfg.ConfigureCredentials(Cert);

    var service = new RpcService3Impl();

    using var server = new RpcService3Quic.Server(service, listener, "Test", logger);

    var port = GetFreeEphemeralTcpPort();

    var ep = new IPEndPoint(IPAddress.IPv6Any, port);

    using var clientCfg = new QuicClientConfiguration(reg, "Test");
    clientCfg.ConfigureCredentials();

    using var clientConnection = new QuicClientConnection(clientCfg);

    listener.Start(ep);

    try {
      var client = new RpcService3Quic.Client(clientConnection, "Test", logger);

      await clientConnection.ConnectAsync("::1", (ushort)ep.Port);

      TestContext.WriteLine("Building message message to send");
      var bb = new BigBufferBuilder();
      Message.StartMessage(bb);
      Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
      Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
      Message.AddBodyType(bb, MessageBody.Empty);
      Message.EndMessage(bb);
      Empty.StartEmpty(bb);
      e.Fill(Empty.EndEmpty(bb));
      subj.Fill("Hello");

      var ct = cts.Token;

      var testExecCtx = TestExecutionContext.CurrentContext;

      var messageReader = new EnumerableChannelReader<Message>(new Message[] { new(0, bb.ByteBuffer), new(0, bb.ByteBuffer) });

      TestContext.WriteLine("Sending 2 Hello messages");

      var result = await client.ClientStreaming(messageReader, ct);

      TestContext.WriteLine("Received a message");

      result.Subject.Should().Be("RE: Hello, Hello");

      TestContext.WriteLine("Received \"Hello\" successfully");
    }
    finally {
      TestContext.WriteLine("Shutting down...");
      clientConnection.Close();
      listener.Stop();
      reg.Shutdown(0);
    }

    TestContext.WriteLine("Disposing...");
  }

  //[Timeout(15000)]
  [Theory]
  [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
  [NonParallelizable]
  public async Task ServerStreamingRoundTrip() {
    var logger = new DebugTestContextWriter(TestContext.Out);

    TaskScheduler.UnobservedTaskException += (_, args) => {
      var aex = args.Exception;
      if (aex.InnerExceptions.Count == 1) {
        var ex = aex.InnerException;
        var exType = ex.GetType();
        if (exType.IsAssignableTo(QuicConnectionAbortedExceptionType)
            || exType.IsAssignableTo(QuicOperationAbortedExceptionType))
          return;
      }

      var report = RecordExceptions(aex).ToString();
      Console.Error.WriteLine(report);
      System.Diagnostics.Debug.WriteLine(report);
      Assert.Fail(report);
    };

    //var bigDelays = run <= 5;

    var testTimeoutMs = 5000;
    var cts = new CancellationTokenSource(testTimeoutMs);

    var testName = TestContext.CurrentContext.Test.FullName;
    using var reg = new QuicRegistration(testName);
    using var listenerCfg = new QuicServerConfiguration(reg, "Test");
    using var listener = new QuicListener(listenerCfg);
    listenerCfg.ConfigureCredentials(Cert);

    var service = new RpcService3Impl();

    using var server = new RpcService3Quic.Server(service, listener, "Test", logger);

    var port = GetFreeEphemeralTcpPort();

    var ep = new IPEndPoint(IPAddress.IPv6Any, port);

    using var clientCfg = new QuicClientConfiguration(reg, "Test");
    clientCfg.ConfigureCredentials();

    using var clientConnection = new QuicClientConnection(clientCfg);

    listener.Start(ep);

    try {
      var client = new RpcService3Quic.Client(clientConnection, "Test", logger);

      await clientConnection.ConnectAsync("::1", (ushort)ep.Port);

      TestContext.WriteLine("Building message message to send");
      var bb = new BigBufferBuilder();
      Message.StartMessage(bb);
      Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
      Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
      Message.AddBodyType(bb, MessageBody.Empty);
      Message.EndMessage(bb);
      Empty.StartEmpty(bb);
      e.Fill(Empty.EndEmpty(bb));
      subj.Fill("Hello");

      TestContext.WriteLine("Sending hello message");

      var ct = cts.Token;

      var testExecCtx = TestExecutionContext.CurrentContext;

      var writer = new EnumerableChannelWriter<Message>();

      await client.ServerStreaming(new(0, bb.ByteBuffer), writer, ct);

      writer.AddingComplete.Should().BeTrue();

      using var messages = writer.AsEnumerable().GetEnumerator();

      messages.MoveNext().Should().BeTrue();
      messages.Current.Subject.Should().Be("RE: Hello");
      messages.MoveNext().Should().BeTrue();
      messages.Current.Subject.Should().Be("RE Ctd.: Hello");
      messages.MoveNext().Should().BeFalse();
    }
    finally {
      TestContext.WriteLine("Shutting down...");
      clientConnection.Close();
      listener.Stop();
      reg.Shutdown(0);
    }

    TestContext.WriteLine("Disposing...");
  }

  [Test]
  //[Timeout(15000)]
  [Theory]
  [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
  [NonParallelizable]
  public async Task BidirectionalStreamingRoundTrip() {
    var logger = new DebugTestContextWriter(TestContext.Out);

    TaskScheduler.UnobservedTaskException += (_, args) => {
      var aex = args.Exception;
      if (aex.InnerExceptions.Count == 1) {
        var ex = aex.InnerException;
        var exType = ex.GetType();
        if (exType.IsAssignableTo(QuicConnectionAbortedExceptionType)
            || exType.IsAssignableTo(QuicOperationAbortedExceptionType))
          return;
      }

      var report = RecordExceptions(aex).ToString();
      Console.Error.WriteLine(report);
      System.Diagnostics.Debug.WriteLine(report);
      Assert.Fail(report);
    };

    //var bigDelays = run <= 5;

    var testTimeoutMs = 5000;
    var cts = new CancellationTokenSource(testTimeoutMs);

    var testName = TestContext.CurrentContext.Test.FullName;
    using var reg = new QuicRegistration(testName);
    using var listenerCfg = new QuicServerConfiguration(reg, "Test");
    using var listener = new QuicListener(listenerCfg);
    listenerCfg.ConfigureCredentials(Cert);

    var service = new RpcService3Impl();

    using var server = new RpcService3Quic.Server(service, listener, "Test", logger);

    var port = GetFreeEphemeralTcpPort();

    var ep = new IPEndPoint(IPAddress.IPv6Any, port);

    using var clientCfg = new QuicClientConfiguration(reg, "Test");
    clientCfg.ConfigureCredentials();

    using var clientConnection = new QuicClientConnection(clientCfg);

    listener.Start(ep);

    try {
      var client = new RpcService3Quic.Client(clientConnection, "Test", logger);

      await clientConnection.ConnectAsync("::1", (ushort)ep.Port);

      TestContext.WriteLine("Building message message to send");
      var bb = new BigBufferBuilder();
      Message.StartMessage(bb);
      Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
      Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
      Message.AddBodyType(bb, MessageBody.Empty);
      Message.EndMessage(bb);
      Empty.StartEmpty(bb);
      e.Fill(Empty.EndEmpty(bb));
      subj.Fill("Hello");

      var ct = cts.Token;

      var reader = new EnumerableChannelReader<Message>(new Message[] { new(0, bb.ByteBuffer), new(0, bb.ByteBuffer) });

      TestContext.WriteLine("Sending 2 Hello messages");

      var writer = new EnumerableChannelWriter<Message>();

      await client.BidirectionalStreaming(reader, writer, ct);

      writer.AddingComplete.Should().BeTrue();

      using var messages = writer.AsEnumerable().GetEnumerator();

      messages.MoveNext().Should().BeTrue();
      messages.Current.Subject.Should().Be("RE: Hello");
      messages.MoveNext().Should().BeTrue();
      messages.Current.Subject.Should().Be("RE: Hello");
      messages.MoveNext().Should().BeFalse();
    }
    finally {
      TestContext.WriteLine("Shutting down...");
      clientConnection.Close();
      listener.Stop();
      reg.Shutdown(0);
    }

    TestContext.WriteLine("Disposing...");
  }

}
