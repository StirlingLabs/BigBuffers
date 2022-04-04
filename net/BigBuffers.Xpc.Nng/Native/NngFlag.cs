using System;

namespace NngNative
{
  [Flags]
  public enum NngFlag : int
  {
    NNG_FLAG_ALLOC = 1,
    NNG_FLAG_NONBLOCK = 2,
  }
}