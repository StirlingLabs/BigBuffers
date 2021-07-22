using StirlingLabs.Utilities;

namespace BigBuffers
{
  public readonly struct VectorPlaceholder<T> where T : unmanaged
  {
    private readonly Placeholder _internal;
    public VectorPlaceholder(BigBufferBuilder bb, ulong offset)
      => _internal = new(bb, offset);

    public VectorPlaceholder(Placeholder placeholder)
      => _internal = placeholder;

    public void Set(T[] s, uint alignment = 0)
      => _internal.Set(s, alignment);
    public void Set(ReadOnlyBigSpan<T> s, uint alignment = 0)
      => _internal.Set(s, alignment);

    public static implicit operator Placeholder(VectorPlaceholder<T> x)
      => x._internal;
  }
}
