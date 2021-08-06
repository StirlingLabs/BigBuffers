#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
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
#if !NETSTANDARD2_0
      [DoesNotReturnIf(false)]
#endif
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
