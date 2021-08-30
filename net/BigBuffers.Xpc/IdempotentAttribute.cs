using System;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  [AttributeUsage(AttributeTargets.Method)]
  public sealed class IdempotentAttribute : Attribute { }
}
