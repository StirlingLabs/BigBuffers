#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using nng;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  public static class NngRpcServiceBase
  {
    private static readonly byte[]? NullByte = { 0 };

    public static ReadOnlySpan<byte> GetBytes<T>(T entity) where T : struct, IBigBufferEntity
      => entity.IsValid()
        ? (ReadOnlySpan<byte>)entity.Model.ByteBuffer.ToReadOnlySpan(0, entity.Model.ByteBuffer.LongLength)
        : throw new NotImplementedException();

#if NETSTANDARD2_0
    private static unsafe ReadOnlySpan<T> CreateReadOnlySpan<T>(ref T item, int length)
      => new(Unsafe.AsPointer(ref item), length);
#endif

    public static INngMsg CreateRequest<T>(this IMessageFactory<INngMsg> factory, ReadOnlySpan<byte> serviceId, ReadOnlySpan<byte> procName,
      T body,
      out long msgId,
      NngMessageType msgType = NngMessageType.Final)
      where T : struct, IBigBufferEntity
    {
      var msg = factory.CreateMessage();
      msgId = SharedCounters.ReadAndIncrementMessageCount();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      var msgIdSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1));
#else
      var msgIdSpan = MemoryMarshal.AsBytes(CreateReadOnlySpan(ref msgId, 1));
#endif
      msg.Append(msgIdSpan);

      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      msg.Append(serviceId);
      msg.Append(stackalloc[] { (byte)':' });
      msg.Append(procName);
      msg.Append(NullByte);
      msg.PadToAlignment(8);
      if (body.IsValid())
        msg.Append(GetBytes(body));
      return msg;
    }

    public static INngMsg CreateStreamingRequest<T>(this IMessageFactory<INngMsg> factory, ReadOnlySpan<byte> serviceId,
      ReadOnlySpan<byte> procName,
      T body,
      long msgId,
      NngMessageType msgType = NngMessageType.Normal)
      where T : struct, IBigBufferEntity
    {
      var msg = factory.CreateMessage();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      var msgIdSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1));
#else
      var msgIdSpan = MemoryMarshal.AsBytes(CreateReadOnlySpan(ref msgId, 1));
#endif
      msg.Append(msgIdSpan);

      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      msg.Append(serviceId);
      msg.Append(stackalloc[] { (byte)':' });
      msg.Append(procName);
      msg.Append(NullByte);
      msg.PadToAlignment(8);
      if (body.IsValid())
        msg.Append(GetBytes(body));
      return msg;
    }
    

    public static INngMsg CreateStreamingRequestContinuation<T>(this IMessageFactory<INngMsg> factory,
      T body,
      long msgId,
      NngMessageType msgType = NngMessageType.Continuation)
      where T : struct, IBigBufferEntity
    {
      var msg = factory.CreateMessage();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      var msgIdSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1));
#else
      var msgIdSpan = MemoryMarshal.AsBytes(CreateReadOnlySpan(ref msgId, 1));
#endif
      msg.Append(msgIdSpan);
      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      if (body.IsValid())
        msg.Append(GetBytes(body));
      return msg;
    }
    public static INngMsg CreateNoMoreFollowsControlRequest(this IMessageFactory<INngMsg> factory, ReadOnlySpan<byte> serviceId,
      ReadOnlySpan<byte> procName,
      long msgId)
    {
      var msg = factory.CreateMessage();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      var msgIdSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1));
#else
      var msgIdSpan = MemoryMarshal.AsBytes(CreateReadOnlySpan(ref msgId, 1));
#endif
      msg.Append(msgIdSpan);

      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { NngMessageType.FinalControl }));
      msg.Append(serviceId);
      msg.Append(stackalloc[] { (byte)':' });
      msg.Append(procName);
      msg.Append(NullByte);
      // no body
      return msg;
    }

    public static INngMsg CreateReply<T>(this IMessageFactory<INngMsg> factory, long msgId, T body,
      NngMessageType msgType = NngMessageType.Final)
      where T : struct, IBigBufferEntity
    {
      var msg = factory.CreateMessage();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      msg.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1)));
#else
      msg.Append(MemoryMarshal.AsBytes(CreateReadOnlySpan(ref msgId, 1)));
#endif
      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      msg.Append(GetBytes(body));
      return msg;
    }

    public static INngMsg CreateControlReply(this IMessageFactory<INngMsg> factory, long msgId,
      NngMessageType msgType = NngMessageType.FinalControl)
    {
      var msg = factory.CreateMessage();
#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
      msg.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1)));
#else
      msg.Append(MemoryMarshal.AsBytes(CreateReadOnlySpan(ref msgId, 1)));
#endif
      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      return msg;
    }
  }
}
