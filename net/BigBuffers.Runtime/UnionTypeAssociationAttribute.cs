using System;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  [AttributeUsage(AttributeTargets.Field)]
  public class UnionTypeAssociationAttribute : Attribute
  {
    public Type Type { get; }

    public UnionTypeAssociationAttribute(Type type)
      => Type = type;
  }
}
