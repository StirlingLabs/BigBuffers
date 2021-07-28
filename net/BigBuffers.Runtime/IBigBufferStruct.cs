namespace BigBuffers
{
  public interface IBigBufferStruct : IBigBufferModel<Struct>
  {
    ulong ByteSize { get; }
  }
}
