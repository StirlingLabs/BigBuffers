using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public sealed class SafeBufferManager : ByteBufferManager, IDisposable
  {
    private byte[] _buffer;
    private unsafe void* _spanPtr;
    private nuint _spanSize;
    private bool _isFixedSize;
    private SafeBuffer _safeBuffer;
    internal unsafe SafeBufferManager(SafeBuffer buffer, bool growable = false)
    {
      _safeBuffer = buffer;
      byte* p = default;
      buffer.AcquirePointer(ref p);
      _spanPtr = p;
      _spanSize = (nuint)buffer.ByteLength;
      _isFixedSize = !growable;
    }

    public override bool Growable
    {
      get => _buffer is not null || !_isFixedSize;
      set => _isFixedSize = !value;
    }

    public override unsafe void GrowFront(ulong newSize)
    {
      if (newSize < LongLength)
        throw new("ByteBuffer: cannot truncate buffer.");

      if (_buffer is null && _isFixedSize)
        throw new InvalidOperationException("Growing the buffer was not permitted.");

      var newBuffer = new byte[newSize];
      Span.CopyTo(new BigSpan<byte>(newBuffer)
        .Slice(0, Span.Length));
      _buffer = newBuffer;
      _spanPtr = default;
      _spanSize = default;
    }

    public override unsafe BigSpan<byte> Span
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get {
        if (_buffer is not null) return (BigSpan<byte>)_buffer;
        return new(_spanPtr, _spanSize);
      }
    }

    public override unsafe ReadOnlyBigSpan<byte> ReadOnlySpan
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get {
        if (_buffer is not null) return (ReadOnlyBigSpan<byte>)_buffer;
        return new(_spanPtr, _spanSize);
      }
    }

    public void Dispose()
    {
      _safeBuffer.ReleasePointer();
      _safeBuffer = null;
    }
  }
}
