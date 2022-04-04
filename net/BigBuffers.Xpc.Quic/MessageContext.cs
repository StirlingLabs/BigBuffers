using System;
using System.Buffers;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

public struct MessageContext
{
  private readonly IQuicRpcServiceContext _serviceCtx;

  private int _sentState;

  private QuicReadOnlyDatagram? _datagram;

  public bool AlreadySent => Interlocked.CompareExchange(ref _sentState, 0, 0) != 0;

  public IQuicRpcServiceContext ServiceContext => _serviceCtx;

  public MessageContext(IQuicRpcServiceContext serviceCtx)
  {
    _serviceCtx = serviceCtx;
    _sentState = 0;
    _datagram = null;
  }


  public Task SendAsync(params ReadOnlyMemory<byte>[] data)
    => SendAsync(false, data);

  public Task SendAsync(bool streamOnly, params ReadOnlyMemory<byte>[] data)
  {
    if (Interlocked.CompareExchange(ref _sentState, 1, 0) != 0)
      throw new InvalidOperationException("Can only be sent once.");

    var connection = _serviceCtx.Connection;

    var frameSize = (ulong)data.Sum(d => d.Length);
    if (!streamOnly || frameSize <= int.MaxValue)
    {
      // tbh should probably never consider sending a datagram over 1500b
      var frameSizeInt = unchecked((int)frameSize);
      if (QuicReadOnlyDatagram.CanCreate(connection, frameSizeInt))
      {
        var memOwner = MemoryPool<byte>.Shared.Rent(frameSizeInt);
        var mem = memOwner.Memory.Slice(0, frameSizeInt);
        var slice = mem.Span;
        foreach (var d in data)
        {
          var s = d.Span;
          s.CopyTo(slice);
          slice = slice.Slice(s.Length);
        }

        if (QuicReadOnlyDatagram.TryCreate(connection, memOwner, mem, out _datagram))
        {
          _datagram!.Send();
          return Task.CompletedTask;
        }
      }
    }

    var frameHeaderSize = VarIntSqlite4.GetEncodedLength(frameSize);
    var frameHeader = new Memory<byte>(new byte[frameHeaderSize]);
    VarIntSqlite4.Encode(frameSize, frameHeader.Span);

    var frame = new ReadOnlyMemory<byte>[data.Length + 1];
    frame[0] = frameHeader;
    for (var i = 0; i < data.Length; i++)
      frame[i + 1] = data[i];

    return _serviceCtx.Stream.SendAsync(frame);
  }
}
