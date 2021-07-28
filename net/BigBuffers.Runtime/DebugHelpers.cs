#nullable enable
using System;
using System.Diagnostics;
using JetBrains.Annotations;

namespace BigBuffers
{
  [PublicAPI]
  internal static class Debug
  {
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [Conditional("DEBUG")]
    [AssertionMethod]
    public static void Assert(
      [AssertionCondition(AssertionConditionType.IS_TRUE)]
      bool condition,
      string? reason = null
    )
    {
      if (condition) return;
      throw new(reason ?? "Assertion failed.");
    }
  }
}
