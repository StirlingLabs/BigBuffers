using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;
using System.Threading.Tasks;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Http
{
  public delegate ValueTask WriteHttpMessageContentAsyncDelegate(Stream stream, TransportContext? context, CancellationToken cancellationToken);

  public sealed class HttpMessageContent : HttpContent
  {
    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    private readonly TextWriter? _logger;
    private readonly AsyncProducerConsumerCollection<WriteHttpMessageContentAsyncDelegate> _writers = new();
    private bool _active;
    public HttpMessageContent(TextWriter? logger = null)
    {
      _logger = logger;
      Headers.ContentType = new("application/x-big-buffers");
    }

    public bool TryAddMessage(byte[] message)
      => _writers.TryAdd((s, _, ct) => {
        ReadOnlySpan<long> header = stackalloc long[] { message.LongLength, 0L };
        s.Write(MemoryMarshal.AsBytes(header));
        return s.WriteAsync(message, ct);
      });

    public bool TryAddMessage(ReadOnlyMemory<byte> message)
      => _writers.TryAdd((s, _, ct) => {
        ReadOnlySpan<long> header = stackalloc long[] { message.Length, 0L };
        s.Write(MemoryMarshal.AsBytes(header));
        return s.WriteAsync(message, ct);
      });

    public bool TryAddMessage<T>(T message) where T : struct, IBigBufferEntity
    {
      if (message.Model.Offset != 0)
        throw new InvalidOperationException("Message entity must be the first entity in the buffer.");

      var bb = message.Model.ByteBuffer;
      var rom = bb.ToSizedReadOnlyMemory();
      return TryAddMessage(rom);
    }

    public ValueTask<bool> TryAddMessageAsync(byte[] message, CancellationToken ct = default)
    {
      try
      {
        if (ct == default) return new(TryAddMessage(message));
        return new(_writers.TryAdd((s, _, ct) => {
          ReadOnlySpan<long> header = stackalloc long[] { message.Length, 0L };
          s.Write(MemoryMarshal.AsBytes(header));
          return s.WriteAsync(message, ct);
        }));
      }
      finally
      {
        if (Volatile.Read(ref _active)) Task.Yield();
      }
    }

    public ValueTask<bool> TryAddMessageAsync<T>(T message, CancellationToken ct = default) where T : struct, IBigBufferEntity
    {
      if (message.Model.Offset != 0)
        throw new InvalidOperationException("Message entity must be the first entity in the buffer.");

      var bb = message.Model.ByteBuffer;
      var rom = bb.ToSizedReadOnlyMemory();
      return TryAddMessageAsync(rom, ct);
    }

    public ValueTask<bool> TryAddMessageAsync(ReadOnlyMemory<byte> message, CancellationToken ct = default)
    {
      try
      {
        if (ct == default) return new(TryAddMessage(message));
        return new(_writers.TryAdd((s, _, writerCt) => {
          var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, writerCt);
          ReadOnlySpan<long> header = stackalloc long[] { message.Length, 0L };
          s.Write(MemoryMarshal.AsBytes(header));
          return s.WriteAsync(message, linked.Token);
        }));
      }
      finally
      {
        if (Volatile.Read(ref _active)) Task.Yield();
      }
    }

    private unsafe ValueTask<bool> TryAddMessageImmediatelyAsync(ReadOnlySpan<byte> message, CancellationToken ct = default)
    {
      if (Volatile.Read(ref _active))
        return new(false);

      if (_writers.IsAddingCompleted)
        return new(false);

      // from this point, will only return false if cancelled or completed adding
      var l = message.Length;
      fixed (byte* pMsg = message)
      {
        var p = (IntPtr)pMsg;
        var wrote = new SemaphoreSlim(0, 1);
        var result = _writers.TryAdd((s, _, _) => {
          var span = new ReadOnlySpan<byte>((void*)p, l);
          ReadOnlySpan<long> header = stackalloc long[] { l, 0L };
          s.Write(MemoryMarshal.AsBytes(header));
          s.Write(span);
          // ReSharper disable once AccessToDisposedClosure
          wrote.Release();
          return ValueTask.CompletedTask;
        });
        if (!result)
          return new(false);

        return new(
          wrote.WaitAsync(ct)
            // ReSharper disable once MethodSupportsCancellation
            .ContinueWith(_ => {
              try { return result; }
              finally { wrote.Dispose(); }
            }));
      }
    }

    public unsafe ValueTask<bool> TryAddMessageAsync(ReadOnlySpan<byte> message, CancellationToken ct = default)
    {
      // zero copy
      var immediate = TryAddMessageImmediatelyAsync(message, ct);

      if (!immediate.IsCompleted)
        // can be false if cancelled or completed adding
        return immediate;

      // ReSharper disable once ConvertIfStatementToReturnStatement
      if (immediate.Result)
        return new(true);

      // needs a copy
      return TryAddMessageAsync(message.ToArray(), ct);
    }

    protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
      => CreateContentReadStreamAsync(cancellationToken).GetAwaiter().GetResult();

    protected override async Task<Stream> CreateContentReadStreamAsync()
      => await CreateContentReadStreamAsync(default);

    protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
    {
      var ms = new MemoryStream();
      await SerializeToStreamAsync(ms, null, cancellationToken);
      return ms;
    }

    protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
      => SerializeToStreamAsync(stream, context, cancellationToken).GetAwaiter().GetResult();

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
      => SerializeToStreamAsync(stream, context, default);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: SerializeToStreamAsync consuming messages");
      await foreach (var writer in _writers.GetConsumer().WithCancellation(cancellationToken))
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: SerializeToStreamAsync writing message");
        await writer(stream, context, cancellationToken);
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: SerializeToStreamAsync wrote message");
      }
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: SerializeToStreamAsync finished consuming messages");

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: SerializeToStreamAsync flushing stream");

      await stream.FlushAsync(cancellationToken);

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: SerializeToStreamAsync flushed stream");
    }

    protected override bool TryComputeLength(out long length)
    {
      Unsafe.SkipInit(out length);
      return false;
    }

    protected override void Dispose(bool disposing)
      => _writers.Dispose();

    public void CompleteAdding()
      => _writers.CompleteAdding();
  }
}
