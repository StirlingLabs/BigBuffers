using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BigBuffers.Xpc.Async;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Http
{
  public abstract class HttpRpcServiceServerBase : INamedRouter
  {
    protected readonly TextWriter? _logger;

    private readonly CancellationToken _cancellationToken;

    private long _routed = 0;

    public HttpRpcServiceServerBase(string name, CancellationToken cancellationToken = default,
      string? basePath = null,
      TextWriter? logger = null)
    {
      Name = name ?? throw new ArgumentNullException(nameof(name));
      _logger = logger;
      _cancellationToken = cancellationToken;
      basePath ??= "/";
      BasePath = !basePath.StartsWith('/')
        ? throw new ArgumentException("Base path must begin with a forward slash (\"/\") character.", nameof(basePath))
        : basePath.TrimEnd('/');
    }

    public string Name { get; }

    public string BasePath { get; }

    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    protected internal TextWriter? Logger => _logger;


    protected virtual Task<ByteBuffer> DispatchUnary<TMethodEnum>(
      TMethodEnum method,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) => Task.FromResult<ByteBuffer>(default);

    protected virtual Task<ByteBuffer> DispatchClientStreaming<TMethodEnum>(
      TMethodEnum method,
      AsyncProducerConsumerCollection<ByteBuffer> reader,
      CancellationToken cancellationToken
    ) => Task.FromResult<ByteBuffer>(default);

    protected virtual Task DispatchServerStreaming<TMethodEnum>(
      TMethodEnum method,
      HttpContext ctx,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected virtual Task DispatchStreaming<TMethodEnum>(
      TMethodEnum method,
      HttpContext ctx,
      AsyncProducerConsumerCollection<ByteBuffer> reader,
      CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected abstract RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method) where TMethodEnum : Enum;

    protected abstract Task Dispatch(string method, HttpContext ctx);

    protected async Task Dispatch<TMethodEnum>(TMethodEnum method, HttpContext ctx) where TMethodEnum : struct, Enum
    {
      var req = ctx.Request;
      var rsp = ctx.Response;

      var proto = req.Protocol;

      async Task<ByteBuffer> ReadUnaryByteBuffer()
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadUnaryByteBuffer");
        var len = req.ContentLength ?? 0;
#if NET6_0_OR_GREATER
        var read = len > 0
          ? await req.BodyReader.ReadAtLeastAsync(checked((int)len), _cancellationToken)
          : await req.BodyReader.ReadAsync(_cancellationToken);
#else
        var read = await req.BodyReader.ReadAsync(_cancellationToken);
#endif
        var mem = !read.Buffer.IsSingleSegment ? read.Buffer.ToArray() : read.Buffer.First;
        ByteBuffer byteBuffer;
        if (MemoryMarshal.TryGetArray(mem, out var arraySeg))
          byteBuffer = new(new Memory<byte>(arraySeg.Array, arraySeg.Offset, arraySeg.Count), 0);
        else
        {
          var newMem = new Memory<byte>(new byte[mem.Length]);
          mem.CopyTo(newMem);
          byteBuffer = new(newMem, 0);
        }
        return byteBuffer;
      }

      async Task<AsyncProducerConsumerCollection<ByteBuffer>> ReadByteBufferStream()
      {
        ctx.AcquireCompletionHold();
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream");
        var messages = new AsyncProducerConsumerCollection<ByteBuffer>();

        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream reading first header");
          await using var stream = req!.BodyReader.AsStream(true);
          var headBuf = new byte[16];
          var bytesRead = await stream.ReadAsync(headBuf, _cancellationToken);
          if (bytesRead == 16)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream read first header");
            var msgSize = MemoryMarshal.Read<long>(headBuf);
            var msgBuf = new byte[checked((int)msgSize)];
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream reading {msgSize} byte message");
            bytesRead = await stream.ReadAsync(msgBuf, _cancellationToken);
            if (bytesRead != msgSize)
            {
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream ending read due to no next header");
              messages.CompleteAdding();
              ctx.ReleaseCompletionHold();
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream completed early 1");
              return messages;
            }
            if (!messages.TryAdd(new(msgBuf)))
            {
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream ending read due to failed to collect first message");
              messages.CompleteAdding();
              //throw new NotImplementedException("Can't add first message?");
              ctx.ReleaseCompletionHold();
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream completed early 2");
              return messages;
            }
          }
        }

        await Task.Factory.StartNew(async () => {
          try
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream reopening body stream");
            var body = req.Body;
            while (body.CanRead)
            {
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream reading next header");

              if (_cancellationToken.IsCancellationRequested)
                return;

              if (messages.IsDisposed || messages.IsAddingCompleted)
                return;
              var headBuf = new byte[16];
              var bytesRead = await body.ReadAsync(headBuf, _cancellationToken);
              if (bytesRead == 0)
              {
                if (ctx.RequestAborted.IsCancellationRequested || _cancellationToken.IsCancellationRequested || !body.CanRead)
                {
                  _logger?.WriteLine(
                    $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream ending read due to no next header");
                  return;
                }
                _logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream ending read due to nothing more to read");
                break;
              }

              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream read next header");

              var msgSize = MemoryMarshal.Read<long>(headBuf);
              var msgBuf = new byte[checked((int)msgSize)];
              bytesRead = await body.ReadAsync(msgBuf, _cancellationToken);
              if (bytesRead != msgSize)
              {
                _logger?.WriteLine(
                  $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream ending read due to incomplete message ({bytesRead} of {msgSize} bytes)");
                messages.CompleteAdding();
                return;
              }
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream read next {msgSize} byte message");

              if (!messages.TryAdd(new(msgBuf)))
                return;
            }

            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream finishing reading messages");
            messages.CompleteAdding();

            body.Close();
            await body.DisposeAsync();

            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream finished");
          }
          finally
          {
            ctx.ReleaseCompletionHold();
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: ReadByteBufferStream completed");

          }
        }, _cancellationToken);

        return messages;
      }

      async Task CleanUp()
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: CleanUp cleaning up request");
        try
        {
          req.BodyReader.CancelPendingRead();
          await req.BodyReader.CompleteAsync();
        }
        catch
        {
          // ok
        }
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: CleanUp cleaning up response");
        try
        {
#if NET6_0_OR_GREATER
          while (rsp.BodyWriter.UnflushedBytes > 0)
            await rsp.BodyWriter.FlushAsync(_cancellationToken);
#else
          await rsp.BodyWriter.FlushAsync(_cancellationToken);
#endif
          rsp.BodyWriter.CancelPendingFlush();
          await rsp.CompleteAsync();
        }
        catch
        {
          // ok
        }
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: CleanUp finished");
        return;
      }

      if (HttpProtocol.IsHttp11(proto))
        ctx.Response.Headers.Add("Connection", "close");
      ctx.Response.OnCompleted(CleanUp);

      switch (ResolveMethodType(method))
      {
        case RpcMethodType.Unary: {
          var msgBuf = await ReadUnaryByteBuffer();
          ByteBuffer reply = default;
          try
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchUnary");
            reply = await DispatchUnary(method, msgBuf, _cancellationToken);
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchUnary Completed");
          }
          catch (Exception ex)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchUnary sending empty error");
            Debug.Assert(!rsp.HasStarted);
            rsp.StatusCode = 500;
            rsp.ContentType = "text/plain; charset=utf-8";
            await rsp.StartAsync(_cancellationToken);
            await rsp.Body.WriteAsync(Encoding.UTF8.GetBytes(ex.ToString()), _cancellationToken);
            await rsp.CompleteAsync();
          }
          if (reply != default)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchUnary sending reply");
            Debug.Assert(!rsp.HasStarted);
            rsp.StatusCode = 200;
            rsp.ContentType = "application/x-big-buffers";
            await rsp.StartAsync(_cancellationToken);
            await using var s = rsp.BodyWriter.AsStream();
            s.Write(reply.ToSizedReadOnlySpan().ToReadOnlySpan());
            await rsp.CompleteAsync();
          }
          else
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchUnary sending empty reply");
            Debug.Assert(!rsp.HasStarted);
            rsp.StatusCode = 204;
            await rsp.StartAsync(_cancellationToken);
            await rsp.CompleteAsync();
          }
          break;
        }
        case RpcMethodType.ClientStreaming: {
          var reqSync = ctx.GetCompletionHoldEvent();
          var c = await ReadByteBufferStream();
          ByteBuffer reply = default;
          try
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchClientStreaming");
            reply = await DispatchClientStreaming(method, c, _cancellationToken);
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchClientStreaming completing");
          }
          catch (Exception ex)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchClientStreaming sending error");
            Debug.Assert(!rsp.HasStarted);
            rsp.StatusCode = 500;
            rsp.ContentType = "text/plain; charset=utf-8";
            await rsp.StartAsync(_cancellationToken);
            await rsp.Body.WriteAsync(Encoding.UTF8.GetBytes(ex.ToString()), _cancellationToken);
            await rsp.CompleteAsync();
            throw;
          }

          if (reply != default)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchClientStreaming sending reply");
            Debug.Assert(!rsp.HasStarted);
            rsp.StatusCode = 200;
            rsp.ContentType = "application/x-big-buffers";
            await rsp.StartAsync(_cancellationToken);
            await using var s = rsp.BodyWriter.AsStream();
            s.Write(reply.ToSizedReadOnlySpan().ToReadOnlySpan());
            await rsp.CompleteAsync();
          }
          else
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchClientStreaming sending empty reply");
            Debug.Assert(!rsp.HasStarted);
            rsp.StatusCode = 204;
            await rsp.StartAsync(_cancellationToken);
            await rsp.CompleteAsync();
          }

          try
          {
            reqSync.Signal();
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchClientStreaming waiting to complete");
            await reqSync.WaitAsync(_cancellationToken);
          }
          catch
          {
            // ok
          }

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{reqSync.CurrentCount}: DispatchClientStreaming completed");
          break;
        }
        case RpcMethodType.ServerStreaming: {
          var reqSync = ctx.GetCompletionHoldEvent();
          var msgBuf = await ReadUnaryByteBuffer();
          ctx.GetCompletionHoldEvent();
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchServerStreaming");
          try
          {
            await DispatchServerStreaming(method, ctx, msgBuf, _cancellationToken);
          }
          catch
          {
            // oof
          }
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{reqSync.CurrentCount}: DispatchServerStreaming waiting to complete");
          try
          {
            reqSync.Signal();
            await reqSync.WaitAsync(_cancellationToken);
          }
          catch
          {
            // ok
          }
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{reqSync.CurrentCount}: DispatchServerStreaming completed");
          break;
        }
        case RpcMethodType.BidirectionalStreaming: {
          var reqSync = ctx.GetCompletionHoldEvent();
          var c = await ReadByteBufferStream();
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: DispatchStreaming");
          await DispatchStreaming(method, ctx, c, _cancellationToken);
          try
          {
            reqSync.Signal();
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{reqSync.CurrentCount}: DispatchStreaming waiting to complete");
            await reqSync.WaitAsync(_cancellationToken);
          }
          catch
          {
            // ok
          }
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{reqSync.CurrentCount}: DispatchStreaming completed");
          break;
        }
        default: throw new NotImplementedException(method.ToString());
      }
    }
    public Task RouteAsync(RouteContext context)
    {
      var httpCtx = context.HttpContext;
      var req = httpCtx.Request;
      var rsp = httpCtx.Response;
      httpCtx.TraceIdentifier += $":{Interlocked.Increment(ref _routed):X}{req.Path}";

      if (req.Method != "POST")
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{httpCtx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{httpCtx.CountCompletionHolds()}: RouteAsync discarding non-POST req");
        return Task.CompletedTask;
      }

      if (req.ContentType != "application/x-big-buffers")
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{httpCtx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{httpCtx.CountCompletionHolds()}: RouteAsync discarding non-big-buffers req");
        return Task.CompletedTask;
      }

      string method;

      if (BasePath == "/")
        method = req.Path.Value!.Substring(1);

      else
      {
        if (!req.Path.StartsWithSegments(BasePath))
          return Task.FromResult(false);

        method = req.Path.Value!.Substring(1 + BasePath.Length);
      }

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId} X{httpCtx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{httpCtx.CountCompletionHolds()}: RouteAsync handling {method}");

      context.RouteData = new() { Routers = { this } };
      context.Handler = async ctx => await Dispatch(method, ctx);

      return Task.CompletedTask;
    }

    public VirtualPathData? GetVirtualPath(VirtualPathContext context)
      => null;

    protected ChannelReader<T> WrapReader<T>(AsyncProducerConsumerCollection<ByteBuffer> r) where T : struct, IBigBufferEntity
      => new EntityHttpMsgChannelReader<T>(r, _logger);

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
