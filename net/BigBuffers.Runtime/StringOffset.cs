namespace BigBuffers
{
  public readonly struct StringOffset : IVectorOffset
  {
    public readonly ulong Value;
    public StringOffset(ulong value)
      => Value = value;
  }
}
