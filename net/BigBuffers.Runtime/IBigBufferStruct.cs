namespace BigBuffers
{
  public interface IBigBufferStruct : IBigBufferEntity
  {
    ulong ByteSize { get; }
    ulong Alignment { get; }
  }
}
