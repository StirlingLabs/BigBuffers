using System;
using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public sealed class ByteArrayManager : ByteBufferManager
  {
    private byte[] _buffer;

    public ByteArrayManager(byte[] buffer)
    {
      Debug.Assert(buffer != null);
      _buffer = buffer;
    }

    public override bool Growable { get => true; set { } }

    public override void GrowFront(ulong newSize)
    {
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
      get => (BigSpan<byte>)_buffer;
    }

    public override ReadOnlyBigSpan<byte> ReadOnlySpan
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => (ReadOnlyBigSpan<byte>)_buffer;
    }
    
    public override bool TryGetMemory(out Memory<byte> seg)
    {
      seg = new(_buffer);
      return true;
    }
  }
}
