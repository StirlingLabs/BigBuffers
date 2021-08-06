using System;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter)]
  public class MetadataIndexAttribute : Attribute
  {
    public ushort Index { get; }

    public MetadataIndexAttribute(ushort index)
      => Index = index;
  }
}
