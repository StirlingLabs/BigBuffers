using System;
using System.Threading;

namespace BigBuffers.Xpc
{
  public static class SharedCounters
  {
#if NET5_0_OR_GREATER
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void Init() {
#if DEBUG
      System.Diagnostics.Debug.WriteLine($"Started: {Started}");
#endif
    }
#endif

    public static readonly DateTime Started = DateTime.Now;

    public static TimeSpan GetTimeSinceStarted() => DateTime.Now - Started;

    private static long _messageCount;

    public static long ReadAndIncrementMessageCount()
      => Interlocked.Increment(ref _messageCount);
  }
}
