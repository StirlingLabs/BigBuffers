using StirlingLabs.Utilities;

namespace BigBuffers
{
  public sealed class ByteArrayAllocator : ByteBufferAllocator
  {
    private byte[] _buffer;
    public ByteArrayAllocator(byte[] buffer)
      => _buffer = buffer;

    public override void GrowFront(ulong newSize)
    {
      if (newSize < LongLength)
        throw new("ByteBuffer: cannot truncate buffer.");

      var newBuffer = new byte[newSize];
      Buffer.CopyTo(new BigSpan<byte>(newBuffer)
        .Slice(0, (nuint)Buffer.LongLength));
      //System.Buffer.BlockCopy(_buffer, 0, newBuffer, newSize - Length, Length);
      _buffer = newBuffer;
    }

    public override BigSpan<byte> Span => (BigSpan<byte>)Buffer;
    public override ReadOnlyBigSpan<byte> ReadOnlySpan => (ReadOnlyBigSpan<byte>)Buffer;

    public override byte[] Buffer => _buffer;
  }
}
