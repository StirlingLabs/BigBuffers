using System;
using StirlingLabs.Utilities;

// @formatter:off
#if NETSTANDARD
using nuint = System.UIntPtr;
using nint = System.IntPtr;
#endif
// @formatter:on

namespace BigBuffers
{
  public sealed class ByteArrayAllocator : ByteBufferAllocator
  {
    public ByteArrayAllocator(byte[] buffer)
    {
      Buffer = buffer;
      InitBuffer();
    }

    public override void GrowFront(ulong newSize)
    {
      if (newSize < LongLength)
        throw new("ByteBuffer: cannot truncate buffer.");

      var newBuffer = new byte[newSize];
      Buffer.CopyTo(new BigSpan<byte>(newBuffer)
        .Slice(0, (nuint)Buffer.LongLength));
      //System.Buffer.BlockCopy(_buffer, 0, newBuffer, newSize - Length, Length);
      Buffer = newBuffer;
      InitBuffer();
    }

    public override BigSpan<byte> Span => (BigSpan<byte>)Buffer;
    public override ReadOnlyBigSpan<byte> ReadOnlySpan => (ReadOnlyBigSpan<byte>)Buffer;

    private void InitBuffer()
      => LongLength = (ulong)Buffer.LongLength;
  }
}
