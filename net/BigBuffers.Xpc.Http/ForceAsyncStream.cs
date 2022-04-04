using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BigBuffers.Xpc.Http
{
  public class ForceAsyncStream : Stream
  {
    private readonly Stream _stream;
    public ForceAsyncStream(Stream stream)
      => _stream = stream;

    public override void Flush()
      => _stream.FlushAsync().GetAwaiter().GetResult();

    public override async Task FlushAsync(CancellationToken cancellationToken)
      => await _stream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count)
      => _stream.ReadAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override long Seek(long offset, SeekOrigin origin)
      => _stream.Seek(offset, origin);

    public override void SetLength(long value)
      => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
      => _stream.WriteAsync(buffer, offset, count).GetAwaiter().GetResult();

    public override void Write(ReadOnlySpan<byte> buffer)
    {
      using var b = MemoryPool<byte>.Shared.Rent(buffer.Length);
      var mem = b.Memory;
      buffer.CopyTo(mem.Span);
      base.WriteAsync(mem).GetAwaiter().GetResult();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
      => await _stream.WriteAsync(buffer, offset, count, cancellationToken);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
      => await _stream.WriteAsync(buffer, cancellationToken);

    public override void Close()
      => _stream.Close();

    public override void CopyTo(Stream destination, int bufferSize)
      => _stream.CopyToAsync(destination, bufferSize).GetAwaiter().GetResult();

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
      => _stream.BeginRead(buffer, offset, count, callback, state);

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
      => _stream.BeginWrite(buffer, offset, count, callback, state);

    public override bool CanTimeout => _stream.CanTimeout;

    public override async ValueTask DisposeAsync()
      => await _stream.DisposeAsync();

    protected override void Dispose(bool disposing)
    {
      _stream.Dispose();
      base.Dispose(disposing);
    }

    public override int Read(Span<byte> buffer)
    {
      using var b = MemoryPool<byte>.Shared.Rent(buffer.Length);
      var mem = b.Memory;
      var result = _stream.ReadAsync(mem).GetAwaiter().GetResult();
      mem.Span.Slice(0, result).CopyTo(buffer);
      return result;
    }

    public override bool CanRead => _stream.CanRead;

    public override bool CanSeek => _stream.CanSeek;

    public override bool CanWrite => _stream.CanWrite;

    public override long Length => _stream.Length;

    public override long Position
    {
      get => _stream.Position;
      set => _stream.Position = value;
    }

    public override int EndRead(IAsyncResult asyncResult)
      => _stream.EndRead(asyncResult);

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
      => await _stream.CopyToAsync(destination, bufferSize, cancellationToken);

    public override void EndWrite(IAsyncResult asyncResult)
      => _stream.EndWrite(asyncResult);

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
      => await _stream.ReadAsync(buffer, offset, count, cancellationToken);

    public override int ReadByte()
    {
      using var b = MemoryPool<byte>.Shared.Rent(1);
      var mem = b.Memory;
      var result = _stream.ReadAsync(mem).GetAwaiter().GetResult();
      if (result <= 0) return -1;
      return mem.Span[0];
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = new CancellationToken())
      => await _stream.ReadAsync(buffer, cancellationToken);

    public override int ReadTimeout
    {
      get => _stream.ReadTimeout;
      set => _stream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
      get => _stream.WriteTimeout;
      set => _stream.WriteTimeout = value;
    }

    public override void WriteByte(byte value)
    {
      using var b = MemoryPool<byte>.Shared.Rent(1);
      var mem = b.Memory;
      mem.Span[0] = value;
      _stream.WriteAsync(mem).GetAwaiter().GetResult();
    }
  }
}
