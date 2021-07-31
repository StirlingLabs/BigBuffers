using System;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  [Serializable]
  public class PlaceholdersUnfilledException : Exception
  {

    private string _message;

    public ImmutableSortedSet<ulong> Offsets;

    public override string Message => _message ?? $"{Offsets.Count} placeholders were unfilled.";

    protected PlaceholdersUnfilledException(SerializationInfo info, StreamingContext context)
      : base(info, context) { }

    internal PlaceholdersUnfilledException(ImmutableSortedSet<ulong> offsets)
      => Offsets = offsets;

    public PlaceholdersUnfilledException(ImmutableSortedSet<ulong> placeholders, string message)
      : this(placeholders)
      => _message = message;
  }
}
