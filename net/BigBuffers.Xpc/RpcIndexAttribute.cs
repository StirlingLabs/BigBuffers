using System;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  [AttributeUsage(AttributeTargets.Method)]
  public sealed class RpcIndexAttribute : Attribute
  {
    public RpcIndexAttribute(int value)
      => Value = value;

    public int Value { get; }
  }
}
