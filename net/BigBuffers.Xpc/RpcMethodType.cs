using System;
using JetBrains.Annotations;

namespace BigBuffers.Xpc
{
  [Flags]
  [PublicAPI]
  public enum RpcMethodType
  {
    Unary = 0,
    ClientStreaming = 1,
    ServerStreaming = 2,
    BidirectionalStreaming = ClientStreaming | ServerStreaming
  }
}
