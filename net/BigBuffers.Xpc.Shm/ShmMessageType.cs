using System;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.Shm
{
  [PublicAPI]
  [Flags]
  public enum ShmMessageType : ulong
  {
    Normal = 0,
    Control = 1 << 0,
    Final = 1 << 1,
    FinalControl = Control | Final,
    Continuation = 1 << 2
  }
}
