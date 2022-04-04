using System;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

public static class MessageHelpers
{
  public static MessageType GetMessageType(BigMemory<byte> raw)
  {
    var span = raw.BigSpan;
    return GetMessageType(span);
  }
  public static MessageType GetMessageType(ReadOnlyBigSpan<byte> span)
  {
    var sizeSize = (uint)VarIntSqlite4.GetDecodedLength(span[0u]);
    var typeSize = (uint)VarIntSqlite4.GetDecodedLength(span[sizeSize]);
    return (MessageType)VarIntSqlite4.Decode((ReadOnlySpan<byte>)span.Slice(sizeSize, typeSize));
  }
}
