namespace BigBuffers
{
  public interface ISchemaModel
  {
    internal ref ByteBufferResidentModel ByteBufferOffset { get; }
    // intentionally blank, only for generic binding
  }
}
