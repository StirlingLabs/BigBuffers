using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace BigBuffers.Xpc.Http
{
  public class HttpMessageStreamWriter<T> : ChannelWriter<T>, IAsyncDisposable, IDisposable
    where T : struct, IBigBufferEntity
  {
    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    private readonly TextWriter? _logger;

    protected long MessageId { get; }

    protected readonly HttpContext Context;

    private object _activeLock = new();
    private Task _active = Task.CompletedTask;

    private bool _completedAdding;

    private bool _disposed;
    private bool? _isHttp11;

    public HttpMessageStreamWriter(HttpContext ctx, TextWriter? logger = null)
    {
      _logger = logger;
      Context = ctx;
      var req = ctx.Request;
      var rsp = ctx.Response;
      ctx.AcquireCompletionHold();
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: created");
    }

    public override async ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
    {
      do
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryWrite(item))
          return;
        cancellationToken.ThrowIfCancellationRequested();
      } while (await WaitToWriteAsync(cancellationToken));

      throw new NotImplementedException();
    }

    public override bool TryWrite(T item)
    {
      var ctx = Context;
      var req = ctx.Request;
      var rsp = ctx.Response;

      if (_disposed)
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: not able to write new message, disposed");
        return false;
      }
      if (_completedAdding)
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: not able to write new message, already completed adding");
        return false;
      }

      if (_active.IsFaulted)
        throw _active.Exception!;
      if (_active.IsCanceled)
        throw new TaskCanceledException(_active);
      if (!_active.IsCompleted)
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: not able to write new message, currently writing");
        return false;
      }

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: sending {typeof(T).Name} stream message (1)");
      /*
      if (_isHttp11 is null)
      {
        var isHttp11 = HttpProtocol.IsHttp11(Context.Request.Protocol);
        _isHttp11 ??= isHttp11;
        if (isHttp11)
          if (!Context.Response.Headers.TryGetValue("Transfer-Encoding", out var te)
            || !te.Any(s => s.Contains("chunked")))
          {
            if (!Context.Response.HasStarted)
            {
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: changing to chunked encoding");
              Context.Response.Headers.Add("Transfer-Encoding", "chunked");
            }
            else
              Debug.Fail("HTTP/1.1 message streams should use chunked transfer encoding.");
          }

        Context.Response.StartAsync().GetAwaiter().GetResult();
      }*/

      var inputMem = item.Model.ByteBuffer.ToSizedMemory();

      var outputMemLength = 16 + inputMem.Length;

      var outputMem = rsp.BodyWriter.GetMemory(outputMemLength);

      lock (_activeLock)
        _active = Task.Run(async () => {
          try
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: writing {typeof(T).Name} stream message header");

            BinaryPrimitives.WriteInt64LittleEndian(outputMem.Span, inputMem.Length);

            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: sending {typeof(T).Name} stream message body");

            inputMem.CopyTo(outputMem.Slice(16));

            rsp.BodyWriter.Advance(outputMemLength);

            await rsp.BodyWriter.FlushAsync();

            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: sent {typeof(T).Name} stream message body");
          }
          finally
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: cleaning up after sending {typeof(T).Name} stream message");

            lock (_activeLock)
              _active = Task.CompletedTask;

            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: cleaned up after sending {typeof(T).Name} stream message");
          }
        });

      return true;
    }

    public override async ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
    {
      if (_disposed) return false;

      if (_completedAdding) return false;

      if (_active is null)
        return true;

      await _active;

      if (_active.IsFaulted)
        throw _active.Exception!;

      if (_active.IsCanceled)
        throw new TaskCanceledException(_active);

      return true;
    }


    public override bool TryComplete(Exception? error = null)
    {
      var ctx = Context;
      var req = ctx.Request;
      var rsp = ctx.Response;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: completing");
      _completedAdding = true;
      return true;
    }

    private async Task CloseAsync()
    {
      var ctx = Context;
      var req = ctx.Request;
      var rsp = ctx.Response;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: closing");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: awaiting active write completion");

      await _active;

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: active write completed");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: finished");
    }

    public async ValueTask DisposeAsync()
    {
      if (_disposed) return;
      _disposed = true;
      var ctx = Context;
      var req = ctx.Request;
      var rsp = ctx.Response;
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: disposing async");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: closing from dispose");
      await CloseAsync();

      ctx.ReleaseCompletionHold();
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ctx.CountCompletionHolds()}: disposed async");
    }

    public void Dispose()
      => DisposeAsync().GetAwaiter().GetResult();
  }
}
