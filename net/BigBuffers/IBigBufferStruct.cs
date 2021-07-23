namespace BigBuffers
{
  public interface IBigBufferStruct : IBigBufferModel
  {
    Struct Model { get; }
    ulong ByteSize { get; }
  }
}