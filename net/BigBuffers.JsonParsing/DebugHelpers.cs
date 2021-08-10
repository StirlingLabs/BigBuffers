#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

  [PublicAPI]
  internal static class Runtime
  {
    [DebuggerHidden]
    [DebuggerNonUserCode]
    [DebuggerStepThrough]
    [AssertionMethod]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
