using System;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  [Flags]
  public enum NngMessageType : ulong
  {
    Normal = 0,
    Control = 1 << 0,
    Final = 1 << 1,
    FinalControl = Control | Final,
    Continuation = 1 << 2
  }
}
