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
    public ImmutableSortedSet<ulong> Offsets;

    protected PlaceholdersUnfilledException(SerializationInfo info, StreamingContext context)
      : base(info, context) { }

    internal PlaceholdersUnfilledException(ImmutableSortedSet<ulong> offsets)
      => Offsets = offsets;

    public override string Message => "Not all placeholders were filled.";
  }
}
