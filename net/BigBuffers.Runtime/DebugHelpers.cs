#nullable enable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IsAtLeastMinimumAlignment(ulong offset, ulong alignment)
    {
      if (!BigBufferBuilder.EnableAlignmentPadding) return;
      if ((offset & (alignment - 1)) != 0)
        throw new("Offset is not at least minimally aligned.");
    }
  }
}
