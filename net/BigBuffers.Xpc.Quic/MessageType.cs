using System;
using JetBrains.Annotations;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
[Flags]
public enum MessageType : ulong
{
  Normal = 0,
  Control = 1 << 0,
  Final = 1 << 1,
  FinalControl = Control | Final,
  Continuation = 1 << 2,
  Reply = 1 << 3,
  FinalControlReply = Control | Final | Reply,
  Exception = 1 << 4
}
