using System;
using nng;
using nng.Native;

namespace BigBuffers.Tests
{
  public sealed class DoNotSendNngMsg : INngMsg
  {
    public static readonly DoNotSendNngMsg Instance = new();

    private DoNotSendNngMsg() { }

    int INngMsgPart.Append(IntPtr data, int size)
      => throw new NotSupportedException();

    int INngMsgPart.Append(ReadOnlySpan<byte> data)
      => throw new NotSupportedException();

    int INngMsgPart.Append(uint data)
      => throw new NotSupportedException();

    int INngMsgPart.Chop(UIntPtr size)
      => throw new NotSupportedException();

    int INngMsgPart.Chop(out uint data)
      => throw new NotSupportedException();

    void INngMsgPart.Clear()
      => throw new NotSupportedException();

    int INngMsgPart.Insert(byte[] data)
      => throw new NotSupportedException();

    int INngMsgPart.Insert(uint data)
      => throw new NotSupportedException();

    int INngMsgPart.Trim(UIntPtr size)
      => throw new NotSupportedException();

    int INngMsgPart.Trim(out uint data)
      => throw new NotSupportedException();

    Span<byte> INngMsgPart.AsSpan()
      => throw new NotSupportedException();

    IntPtr INngMsgPart.AsPtr()
      => throw new NotSupportedException();

    int INngMsgPart.Length
      => throw new NotSupportedException();

    void IDisposable.Dispose()
      => throw new NotSupportedException();

    nng_msg INngMsg.Take()
      => throw new NotSupportedException();

    NngResult<INngMsg> INngMsg.Dup()
      => throw new NotSupportedException();

    nng_msg INngMsg.NativeNngStruct
      => throw new NotSupportedException();

    INngMsgPart INngMsg.Header
      => throw new NotSupportedException();

    INngPipe INngMsg.Pipe
      => throw new NotSupportedException();
  }
}
