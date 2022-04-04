using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public abstract class QuicRpcServiceContext : IQuicRpcServiceContext
{
  protected readonly TextWriter? Logger;

  [Discardable]
  protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

  protected QuicRpcServiceContext(QuicPeerConnection connection, TextWriter? logger, bool isServer)
  {
    Logger = logger;
    Connection = connection;
    Connection.DatagramReceived += DatagramReceivedHandler;

    Connection.IncomingStream += isServer
      ? ServerIncomingStreamHandler
      : ClientIncomingStreamHandler;

    ControlStreamOutbound = connection.OpenUnidirectionalStream();
  }

  public QuicPeerConnection Connection { get; }

  public QuicStream? ControlStreamOutbound { get; private set; }
  public QuicStream? ControlStreamInbound { get; private set; }

  QuicStream IQuicRpcServiceContext.Stream => ControlStreamOutbound
    ?? throw new NotImplementedException("early control stream outbound needed? trigger creation here?");

  public ConcurrentBijectiveSet<long, QuicStream> RpcStreamsOutbound { get; } = new();
  public ConcurrentBijectiveSet<long, QuicStream> RpcStreamsInbound { get; } = new();
  public ConcurrentDictionary<long, BigMemory<byte>> RpcMessageBuffersInbound { get; } = new();

  public ConcurrentQueue<IMessage> ReceivedMessageQueue { get; } = new();


  private void RpcStreamDataReceivedHandler(QuicStream quicStream, Action<BigMemory<byte>> messageReceived)
  {
    Span<byte> varIntBuf = stackalloc byte[9];
    var dataAvailable = quicStream.DataAvailable;
    var continued = false;

    for (;;)
    {
      if (continued || !RpcMessageBuffersInbound.TryGetValue(quicStream.Id, out var buf))
      {
        var received = (nuint)quicStream.Receive(varIntBuf.Slice(0, 1));
        dataAvailable -= (uint)received;
        var sizeSize = (uint)VarIntSqlite4.GetDecodedLength(varIntBuf[0]);
        if (dataAvailable <= sizeSize)
        {
          // incomplete buffer
          buf = new(9);
          buf.Resize(dataAvailable);
          RpcMessageBuffersInbound[quicStream.Id] = buf;
          return;
        }
        received = (nuint)quicStream.Receive(varIntBuf.Slice(1, (int)(sizeSize - 1)));
        dataAvailable -= (uint)received;
        var bodySize = checked((int)VarIntSqlite4.Decode(varIntBuf));
        var frameSize = bodySize + sizeSize;

        buf = new((nuint)frameSize);
        received = (nuint)quicStream.Receive(buf.BigSpan.Slice(sizeSize, bodySize));
        dataAvailable -= (uint)received;

        if (received < (uint)bodySize)
        {
          // incomplete buffer
          buf.Resize(sizeSize + received);
          RpcMessageBuffersInbound[quicStream.Id] = buf;
          return;
        }
        messageReceived(buf);
        RpcMessageBuffersInbound.TryRemove(quicStream.Id, out var _);
        // parse remaining buffer
        continued = true;

      }
      else
      {
        Debug.Assert(buf.Length > 0);
        var sizeSize = (uint)VarIntSqlite4.GetDecodedLength(buf.BigSpan[0u]);
        nuint received;
        var restart = buf.Length;
        if (restart < sizeSize)
        {
          var remaining = sizeSize - restart;
          buf.Resize(sizeSize);
          received = quicStream.Receive(buf.BigSpan.Slice(restart, remaining));
          dataAvailable -= (uint)received;
          if (received < remaining)
          {
            // incomplete buffer
            buf.Resize(restart + received);
            //RpcMessageBuffersInbound[quicStream.Id] = buf; // already in
            return;
          }
        }
        var bodySize = checked((int)VarIntSqlite4.Decode((ReadOnlySpan<byte>)buf.BigSpan.Slice(0, sizeSize)));
        var frameSize = (nuint)bodySize + sizeSize;
        buf.Resize(frameSize);
        received = (nuint)quicStream.Receive(buf.BigSpan.Slice(sizeSize, bodySize));
        dataAvailable -= (uint)received;
        if (received < (uint)bodySize)
        {
          // incomplete buffer
          buf.Resize(sizeSize + received);
          //RpcMessageBuffersInbound[quicStream.Id] = buf; // already in
          return;
        }
        messageReceived(buf);
        // parse remaining buffer
        continued = true;
      }
    }
  }
  protected bool HandleMessage<TMessage>(IMessage msg, out TMessage result, bool unary) where TMessage : struct, IBigBufferTable
  {
    var msgType = msg.Type;

    if ((msgType & MessageType.Control) != 0)
    {
      if ((msgType & MessageType.Final) == 0)
      {
        if (unary) // non-final control request reply to unary method
          throw new("Unknown final control request.")
            { Data = { [typeof(IMessage)] = msg } };
        throw new("Unknown non-final control request.")
          { Data = { [typeof(IMessage)] = msg } };
      }

      if (msg.Body.Length == 0)
      {
        // final control request reply with no body to non-unary method is the end of the messages
        Unsafe.SkipInit(out result);
        return false;
      }

      if (unary) // final control request to unary method is an error
        throw new("The service was unable to handle the request.")
          { Data = { [typeof(IMessage)] = msg } };

      // non-final control request to non-unary method is not implemented
      throw new("Unknown non-final control request.")
        { Data = { [typeof(IMessage)] = msg } };
    }

    if (unary)
      if ((msgType & MessageType.Final) == 0)
        throw new("The service had more than one result.")
          { Data = { [typeof(IMessage)] = msg } };

    var body = msg.Body;
    if (body.Length > 0)
    {
      ByteBuffer bb = new(body);
      result = new()
      {
        Model = new(bb, bb.Position)
      };
      return true;
    }

    Unsafe.SkipInit(out result);
    return false;
  }
  private void ClientIncomingStreamHandler(QuicPeerConnection sender, QuicStream stream)
  {
    if (ControlStreamInbound is null)
    {
      ControlStreamInbound = stream;
      stream.DataReceived += RpcControlStreamDataReceivedHandler;
    }
    if (RpcStreamsInbound.TryAdd(stream.Id, stream))
      stream.DataReceived += RpcReplyMessageStreamDataReceivedHandler;
    else
      Debug.Fail("Failed to add RPC Stream, already existed in collection?");
  }
  private void ServerIncomingStreamHandler(QuicPeerConnection sender, QuicStream stream)
  {
    if (ControlStreamInbound is null)
    {
      ControlStreamInbound = stream;
      stream.DataReceived += RpcControlStreamDataReceivedHandler;
    }
    if (RpcStreamsInbound.TryAdd(stream.Id, stream))
      stream.DataReceived += RpcRequestMessageStreamDataReceivedHandler;
    else
      Debug.Fail("Failed to add RPC Stream, already existed in collection?");
  }

  private void RpcControlStreamDataReceivedHandler(QuicStream quicStream)
    => RpcStreamDataReceivedHandler(quicStream, buf => {
      var type = MessageHelpers.GetMessageType(buf);
      ReceivedMessageQueue.Enqueue(
        (type & MessageType.Reply) != 0
          ? new ReplyMessage(buf, false)
          : new RequestMessage(buf, false));
    });

  private void RpcRequestMessageStreamDataReceivedHandler(QuicStream quicStream)
    => RpcStreamDataReceivedHandler(quicStream, buf => {
      ReceivedMessageQueue.Enqueue(new RequestMessage(buf, true)
        { Id = RpcStreamsInbound[quicStream] });
    });

  private void RpcReplyMessageStreamDataReceivedHandler(QuicStream quicStream)
    => RpcStreamDataReceivedHandler(quicStream, buf => {
      ReceivedMessageQueue.Enqueue(new ReplyMessage(buf, true)
        { Id = RpcStreamsInbound[quicStream] });
    });

  private void DatagramReceivedHandler(QuicPeerConnection sender, ReadOnlySpan<byte> data)
  {
    var mem = new BigMemory<byte>((nuint)data.Length);
    data.CopyTo(mem.BigSpan);
    var msgType = MessageHelpers.GetMessageType(mem);
    ReceivedMessageQueue.Enqueue((msgType & MessageType.Reply) != 0
      ? new ReplyMessage(mem, false)
      : new RequestMessage(mem, false));
  }
}
