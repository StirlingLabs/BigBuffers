using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BigBuffers.Xpc.Async;
using JetBrains.Annotations;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Collections;
using ZeroMQ;

// TODO: logging of messageId

namespace BigBuffers.Xpc.ZeroMq
{
  [PublicAPI]
  public abstract class ZeroMqRpcServiceServerBase
  {
    protected readonly TextWriter? _logger;

    private readonly CancellationToken _cancellationToken;

    private long _routed = 0;

    private static readonly Regex RxSplitPascalCase = new("(?<=[a-z])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly byte[]
      Utf8ErrorBadRequest = Encoding.UTF8.GetBytes("Bad Request"),
      Utf8ErrorUnauthorized = Encoding.UTF8.GetBytes("Unauthorized"),
      Utf8ErrorNotFound = Encoding.UTF8.GetBytes("Not Found"),
      Utf8ErrorRequestTimeout = Encoding.UTF8.GetBytes("Request Timeout"),
      Utf8ErrorGone = Encoding.UTF8.GetBytes("Gone"),
      Utf8ErrorNotImplemented = Encoding.UTF8.GetBytes("Not Implemented"),
      Utf8ErrorInternalServerError = Encoding.UTF8.GetBytes("Internal Server Error");

    public ZeroMqRpcServiceServerBase(string name,
      ZSocket socket,
      CancellationToken cancellationToken = default,
      string? basePath = null, TextWriter? logger = null)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
      Socket = socket ?? throw new ArgumentNullException(nameof(socket));
      _logger = logger;
      _cancellationToken = cancellationToken;
      basePath ??= "/";
#if NETSTANDARD2_0
      BasePath = !basePath.StartsWith("/")
#else
      BasePath = !basePath.StartsWith('/')
#endif
        ? throw new ArgumentException("Base path must begin with a forward slash (\"/\") character.", nameof(basePath))
        : basePath.TrimEnd('/');
    }

    public string Name { get; }

    public string BasePath { get; }

    public ZSocket Socket { get; }

    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    protected internal TextWriter? Logger => _logger;


    protected Task RunAsync<TMethodEnum>(CancellationToken cancellationToken) where TMethodEnum : struct, Enum
    {
      // used as a concurrent set
      ConcurrentDictionary<Task, _> dispatched = new();

      async Task DispatcherLoop()
      {
        try
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}: dispatcher loop started");
          while (!cancellationToken.IsCancellationRequested)
          {
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}: listening for new request");

            var header = Socket.ReceiveFrame();
            if (!TryParseHeaderFrame(header))
            {
              Socket.dr
              continue;
            }
            var content = Socket.ReceiveFrame();

            if (cancellationToken.IsCancellationRequested) break;

#if NET5_0_OR_GREATER
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}: received request: 0x{Convert.ToHexString(request.AsSpan())}");
#endif
            var sync = new SemaphoreSlim(0, 1);
            var runner = Task.Run(async () => {
              _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatched request started");
              await Dispatch<TMethodEnum>(Pair.Id, request, sync, cancellationToken);
              _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatched request completed");
            }, cancellationToken);
            var added = dispatched.TryAdd(runner, default);

            Debug.Assert(added);

            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: synchronizing with dispatch task T{runner.Id}");

            try
            {
              await sync.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
              Debug.Assert(sync.CurrentCount == 0);
              if (cancellationToken.IsCancellationRequested) break;
            }

            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: synchronized");

            // sweep outstanding tasks to prevent leakage over long runtime
            foreach (var kvp in dispatched)
            {
              if (!kvp.Key.IsCompleted)
                continue;

              dispatched.TryRemove(kvp.Key, out var _);
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: collected completed dispatch request T{kvp.Key.Id}");
            }
          }
        }
        finally
        {

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatcher loop ending, finished dispatching, waiting on outstanding dispatches");
          // wait on any remaining outstanding tasks
          await Task.WhenAll(dispatched.Keys);

          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatcher loop ended, no more outstanding dispatches");

        }
      }

      return Task.Run(DispatcherLoop, cancellationToken);
    }

    private bool TryParseHeaderFrame(ZFrame header, out long messageId, out string? locator)
    {
      if (header.Length < 8)
      {
        #if NETSTANDARD2_0
        messageId = default;
        locator = default;
        #else
        Unsafe.SkipInit(out messageId);
        Unsafe.SkipInit(out locator);
        #endif
        return false;
      }
      messageId = header.ReadInt64();
      locator = header.Length > 8 ? header.ReadString() : null;
      return true;
    }


    protected virtual Task<ByteBuffer> DispatchUnary<TMethodEnum>(
      TMethodEnum method,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) => Task.FromResult<ByteBuffer>(default);

    protected virtual Task<ByteBuffer> DispatchClientStreaming<TMethodEnum>(TMethodEnum method,
      AsyncProducerConsumerCollection<(ByteBuffer Buffer, IDisposable Lifetime)> reader,
      CancellationToken cancellationToken) => Task.FromResult<ByteBuffer>(default);

    protected virtual Task DispatchServerStreaming<TMethodEnum>(
      TMethodEnum method,
      long messageId,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected virtual Task DispatchStreaming<TMethodEnum>(TMethodEnum method,
      long messageId,
      AsyncProducerConsumerCollection<(ByteBuffer Buffer, IDisposable Lifetime)> reader,
      CancellationToken cancellationToken) => Task.CompletedTask;

    protected abstract RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method) where TMethodEnum : Enum;

    protected abstract Task Dispatch(string method, ZFrame frame);

    protected readonly ConcurrentDictionary<long, (uint RoutingId, AsyncProducerConsumerCollection<(ByteBuffer Buffer, IDisposable Lifetime)>
        Messages)>
      StreamContexts = new();

    protected async Task Dispatch<TMethodEnum>(TMethodEnum method, ZFrame frame) where TMethodEnum : struct, Enum
    {
      if (!frame.TryGetRoutingId(out var routingId))
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name}: Dispatch failed to get routing id from ZFrame.");
        return;
      }

      unsafe ByteBuffer ReadByteBuffer(out long messageId, out long flags)
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name}: ReadByteBuffer");

        var len = frame.GetLength();
        messageId = frame.ReadInt64();
        flags = frame.ReadInt64();
        var byteBuffer = new ByteBuffer(new BigSpan<byte>(frame.Data() + 16, len));

        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: ReadByteBuffer done");

        return byteBuffer;
      }

      AsyncProducerConsumerCollection<(ByteBuffer Buffer, IDisposable Lifetime)>? ReadOrAddToByteBufferStream(out long messageId,
        out long flags)
      {

        var byteBuffer = ReadByteBuffer(out messageId, out flags);

        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: ReadByteBufferStream");

        var created = false;

        var ctx = StreamContexts.GetOrAdd(messageId, mId => {
          created = true;
          var messages = new AsyncProducerConsumerCollection<(ByteBuffer Buffer, IDisposable Lifetime)>();

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{mId}: ReadByteBufferStream adding first frame.");

          if (messages.TryAdd((byteBuffer, frame)))
            return (routingId, messages);

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{mId}: ReadByteBufferStream failed to add the first frame.");
          throw new InvalidOperationException();

        });

        return created ? ctx.Messages : null;
      }

      switch (ResolveMethodType(method))
      {
        case RpcMethodType.Unary: {
          var msgBuf = ReadByteBuffer(out var messageId, out _);
          ByteBuffer reply = default;

          try
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchUnary");
            reply = await DispatchUnary(method, msgBuf, _cancellationToken);
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchUnary Completed");
          }
          catch (Exception ex)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchUnary sending empty error");

            SendException(messageId, ex);
            throw;
          }
          if (reply != default)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchUnary sending reply");
#if NETSTANDARD2_0
            Socket.SendBytes(MemoryMarshal.AsBytes(stackalloc long[] { messageId }), 0, 8);
#else
            Socket.SendBytes(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref messageId, 1)), 0, 8);
#endif
            Socket.SendFrame(ZFrame.Create((ReadOnlySpan<byte>)reply.ToSizedReadOnlySpan()));
          }
          else
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchUnary sending empty reply");
#if NETSTANDARD2_0
            Socket.SendBytes(MemoryMarshal.AsBytes(stackalloc long[] { messageId }), 0, 8);
#else
            Socket.SendBytes(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref messageId, 1)), 0, 8);
#endif
            Socket.SendFrame(ZFrame.CreateEmpty());
          }
          break;
        }
        case RpcMethodType.ClientStreaming: {
          var c = ReadOrAddToByteBufferStream(out var messageId, out _);
          if (c is null) break;

          ByteBuffer reply = default;
          try
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchClientStreaming");
            reply = await DispatchClientStreaming(method, c, _cancellationToken);
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchClientStreaming completing");
          }
          catch (Exception ex)
          {
            SendException(messageId, ex);
            throw;
          }

          if (reply != default)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchClientStreaming sending reply");
            Socket.SendBytes(MemoryMarshal.AsBytes(stackalloc long[2] { messageId, 0 }), 0, 16);
            Socket.SendFrame(ZFrame.Create((ReadOnlySpan<byte>)reply.ToSizedReadOnlySpan()));
          }
          else
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchClientStreaming sending empty reply");
            Socket.SendBytes(MemoryMarshal.AsBytes(stackalloc long[2] { messageId, 0 }), 0, 16);
            Socket.SendFrame(ZFrame.CreateEmpty());
          }

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchClientStreaming completed");
          break;
        }
        case RpcMethodType.ServerStreaming: {
          var msgBuf = ReadByteBuffer(out var messageId, out _);
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchServerStreaming");
          try
          {
            await DispatchServerStreaming(method, messageId, msgBuf, _cancellationToken);
          }
          catch
          {
            // oof
          }
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchServerStreaming completed");
          break;
        }
        case RpcMethodType.BidirectionalStreaming: {
          var c = ReadOrAddToByteBufferStream(out var messageId, out _);
          if (c is null) break;
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchStreaming");
          await DispatchStreaming(method, messageId, c, _cancellationToken);
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{Socket.Name} #{messageId}: DispatchStreaming completed");
          break;
        }
        default: throw new NotImplementedException(method.ToString());
      }
    }
    private void SendException(long messageId, Exception ex)
    {
      Socket.SendBytes(MemoryMarshal.AsBytes(stackalloc long[2] { messageId, 1 }), 0, 16);
      var bytes = Encoding.UTF8.GetBytes(ex.ToString());
      Socket.SendBytes(bytes, 0, (nuint)bytes.Length);
    }

    protected ZFrame FinalControlReply(long msgId, long errorCode, ReadOnlySpan<byte> message)
    {
      var response = ZFrame.Create();
      response.Write(msgId);
      response.Write(errorCode);
      response.Write(message);
      response.WriteByte(0);
      return response;
    }

    private void AddReplyExceptionMessage(ZFrame reply, Exception? ex = null)
    {
      if (ex is null) return;
      reply.Write(Encoding.UTF8.GetBytes($"{ex.GetType().Name}: {ex.Message}"));
      reply.WriteByte(0);
    }
    protected ZFrame BadRequestReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 400, Utf8ErrorBadRequest);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected ZFrame UnauthorizedReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 401, Utf8ErrorUnauthorized);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected ZFrame NotFoundReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 404, Utf8ErrorNotFound);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected ZFrame TimedOutReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 408, Utf8ErrorRequestTimeout);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected ZFrame GoneReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 410, Utf8ErrorGone);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected ZFrame NotImplementedExceptionReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 501, Utf8ErrorNotImplemented);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

#if NET5_0_OR_GREATER
    protected ZFrame UnhandledHttpExceptionReply(long msgId, HttpRequestException ex)
    {
      // ReSharper disable once ConstantNullCoalescingCondition
      ex ??= new("An exception was not provided.");
      var statusCode = ex.StatusCode ?? HttpStatusCode.InternalServerError;
      var message = RxSplitPascalCase.Replace(statusCode.ToString(), " $1");
      var reply = FinalControlReply(msgId, (long)statusCode, Encoding.UTF8.GetBytes(message));
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }
#endif
    protected ZFrame UnhandledExceptionReply(long msgId, Exception ex)
    {
      var reply = FinalControlReply(msgId, 500, Utf8ErrorInternalServerError);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected ChannelReader<T> WrapReader<T>(AsyncProducerConsumerCollection<ByteBuffer> r) where T : struct, IBigBufferEntity
      => new EntityZeroMqFrameChannelReader<T>(r, _logger);

    protected async Task<ByteBuffer> Reply<T>(Task<T> task) where T : struct, IBigBufferEntity
      => (await task).Model.ByteBuffer;

    protected virtual ByteBuffer? OnUnhandledMessage(ByteBuffer msg, CancellationToken cancellationToken)
      => OnUnhandledMessage(cancellationToken);

    protected virtual ByteBuffer? OnUnhandledMessage(CancellationToken cancellationToken)
      => null;

    protected static T Track<T>(T disposable, ICollection<IAsyncDisposable> collection) where T : IAsyncDisposable
    {
      collection.Add(disposable);
      return disposable;
    }
  }
}
