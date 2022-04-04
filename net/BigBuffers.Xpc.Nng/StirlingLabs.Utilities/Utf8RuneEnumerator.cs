#if NET5_0_OR_GREATER
using System;
using System.Buffers;
using System.Text;
using JetBrains.Annotations;

// TODO: move to StirlingLabs.Utilities
namespace StirlingLabs.Utilities
{
  [PublicAPI]
  public ref struct Utf8RuneEnumerator
  {
    private ReadOnlySpan<byte> _remaining;
    private Rune _current;

    public Utf8RuneEnumerator(ReadOnlySpan<byte> buffer)
    {
      _remaining = buffer;
      _current = default;
    }

    public Rune Current => _current;

    public Utf8RuneEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
      if (_remaining.IsEmpty)
      {
        // reached the end of the buffer
        _current = default;
        return false;
      }

      var status = Rune.DecodeFromUtf8(_remaining, out _current, out var bytesRead);

      if (status == OperationStatus.InvalidData)
      {
        _remaining = default;
        throw new InvalidOperationException("Invalid data encountered in UTF8 string.");
      }

      if (bytesRead == 0)
      {
        _remaining = default;
        return false;
      }

      _remaining = _remaining.Slice(bytesRead);
      return true;
    }
  }
}
#endif
