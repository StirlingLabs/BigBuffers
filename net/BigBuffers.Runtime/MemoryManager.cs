using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public sealed class MemoryManager : ByteBufferManager
  {
    private Memory<byte> _buffer;
    private bool _isFixedSize;

    public MemoryManager(Memory<byte> buffer, bool isFixedSize)
    {
      _buffer = buffer;
      _isFixedSize = isFixedSize;
    }

    public override bool Growable { get => _isFixedSize; set { } }

    public override void GrowFront(ulong newSize)
    {
      if (_isFixedSize)
        throw new InvalidOperationException("Growing the buffer was not permitted.");

      if (newSize < LongLength)
        throw new("ByteBuffer: cannot truncate buffer.");

      var newBuffer = new byte[newSize];
      Span.CopyTo(new BigSpan<byte>(newBuffer)
        .Slice(0, Span.Length));
      _buffer = newBuffer;
    }

    public override BigSpan<byte> Span
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _buffer.Span;
    }

    public override ReadOnlyBigSpan<byte> ReadOnlySpan
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _buffer.Span;
    }

    public override bool TryGetMemory(out Memory<byte> seg)
    {
      seg = _buffer;
      return true;
    }
  }
}
