using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public class ByteBufferDebugger
  {
    private readonly ByteBuffer _bb;
    public ByteBufferDebugger(ByteBuffer bb)
      => _bb = bb;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public ReadOnlySpan<byte> DebugView
      => (ReadOnlySpan<byte>)ReadOnlyBigSpan.Create(ref _bb.RefByte(0), _bb.Length);
  }
}
