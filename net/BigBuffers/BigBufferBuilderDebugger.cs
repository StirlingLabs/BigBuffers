using System;
using System.Diagnostics;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public class BigBufferBuilderDebugger
  {
    private readonly BigBufferBuilder _bb;
    public BigBufferBuilderDebugger(BigBufferBuilder bb)
      => _bb = bb;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public ReadOnlySpan<byte> DebugView
      => (ReadOnlySpan<byte>)ReadOnlyBigSpan.Create(ref _bb.ByteBuffer.RefByte(0), (nuint)_bb.Offset);
  }
}
