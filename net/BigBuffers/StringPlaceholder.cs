namespace BigBuffers
{
  public readonly struct StringPlaceholder
  {
    private readonly Placeholder _internal;
    public StringPlaceholder(BigBufferBuilder bb, ulong offset)
      => _internal = new(bb, offset);

    public StringPlaceholder(Placeholder placeholder)
      => _internal = placeholder;

    public void Set(string s)
      => _internal.Set(s);

    public static implicit operator Placeholder(StringPlaceholder x)
      => x._internal;
  }
}
