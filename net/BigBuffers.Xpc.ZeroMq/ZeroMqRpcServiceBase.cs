using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using BigBuffers.Xpc.Async;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.ZeroMq
{
  [PublicAPI]
  public static class ZeroMqRpcServiceBase
  {
    [Discardable]
    private static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;
  }
}
