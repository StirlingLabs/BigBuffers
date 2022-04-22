using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public class RequestMessage : IMessage
{
  private MessageContext? _context;

  public IQuicRpcServiceContext? Context
  {
    get => _context?.ServiceContext;
    set => _context = value is not null ? new MessageContext(value) : null;
  }

  public MessageType Type { get; set; }
  public long Id { get; set; }
  public BigMemory<byte> Raw { get; }
  public bool StreamOnly { get; set; }
  public uint HeaderSize { get; set; }
  public nuint BodySize => Raw.Length - HeaderSize;
  public BigSpan<byte> Header => HeaderSize == 0 ? BigSpan<byte>.Empty : Raw.BigSpan.Slice(0, HeaderSize);
  public BigSpan<byte> Body => HeaderSize == 0 ? Raw.BigSpan : Raw.BigSpan.Slice(HeaderSize);
  public SizedUtf8String ServiceId { get; set; }
  public SizedUtf8String RpcMethod { get; set; }

  public nuint LocatorSize => ServiceId.Length + RpcMethod.Length + 1;

  public ref byte GetPinnableReference()
    => ref Raw.GetPinnableReference();

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public RequestMessage(IQuicRpcServiceContext serviceCtx, MessageType type, BigMemory<byte> raw, long id)
  {
    if (serviceCtx is null)
      throw new ArgumentNullException(nameof(serviceCtx));
    _context = new(serviceCtx);
    Id = id;
    Type = type;
    Raw = raw;
    StreamOnly = false;
    HeaderSize = 0;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public RequestMessage(BigMemory<byte> raw, bool withoutLocator)
  {
    _context = null;

    var span = raw.BigSpan;
    var typeSize = (uint)VarIntSqlite4.GetDecodedLength(span[0u]);
    Type = (MessageType)VarIntSqlite4.Decode((ReadOnlySpan<byte>)span.Slice(0, typeSize));
    var idSize = (uint)VarIntSqlite4.GetDecodedLength(span[typeSize]);
    Id = (long)VarIntSqlite4.Decode((ReadOnlySpan<byte>)span.Slice(typeSize, idSize));

    var headerSize = typeSize + idSize;

    if (!withoutLocator)
    {
      var locSizeSize = (uint)VarIntSqlite4.GetDecodedLength(span[headerSize]);
      var locSize = (uint)VarIntSqlite4.Decode((ReadOnlySpan<byte>)span.Slice(headerSize, locSizeSize));
      var locStart = headerSize + locSizeSize;
      var locSpan = span.Slice(locStart, locSize);
      headerSize += locSizeSize + locSize;

      nuint rpcMethodStart = 0;

      //var locLen = headerSize - locStart;

      for (var i = 0u; i < locSize; ++i)
      {
        if (locSpan[i] != ':')
          continue;
        rpcMethodStart = locStart + i + 1;
        break;
      }

      // if missing ':', service id and rpc method name are same
      if (rpcMethodStart == 0)
      {
        var locatorLen = headerSize - locStart;
        var locator = Utf8String.Create(MemoryMarshal.Cast<byte, sbyte>((ReadOnlySpan<byte>)span.Slice(locStart, locatorLen)));
        RpcMethod = ServiceId = new(locatorLen, locator);
      }
      else
      {
        var svcIdLen = rpcMethodStart - locStart - 1;
        var rpcMethodLen = headerSize - rpcMethodStart;

        var serviceId = Utf8String.Create(MemoryMarshal.Cast<byte, sbyte>((ReadOnlySpan<byte>)span.Slice(locStart, svcIdLen)));
        ServiceId = new(svcIdLen, serviceId);

        var rpcMethod = Utf8String.Create(MemoryMarshal.Cast<byte, sbyte>((ReadOnlySpan<byte>)span.Slice(rpcMethodStart, rpcMethodLen)));
        RpcMethod = new(rpcMethodLen, rpcMethod);
      }
    }

    HeaderSize = headerSize;
    Raw = raw;
    StreamOnly = false;
  }

  public Task SendAsync()
  {
    var ctx = _context ?? throw new InvalidOperationException("Message is not able to be sent.");

    if (ctx.AlreadySent) throw new InvalidOperationException("Already sent.");

    // type, id, locator

    var typeLen = VarIntSqlite4.GetEncodedLength((ulong)Type);
    var idLen = VarIntSqlite4.GetEncodedLength((ulong)Id);
    var locSize = LocatorSize;
    var locSizeLen = VarIntSqlite4.GetEncodedLength(locSize);
    var headerSize = idLen + typeLen + locSizeLen + (int)locSize;
    var header = new Memory<byte>(new byte[headerSize]);

    var s = header.Span;
    VarIntSqlite4.Encode((ulong)Type, s);
    VarIntSqlite4.Encode((ulong)Id, s.Slice(typeLen));
    VarIntSqlite4.Encode(locSize, s.Slice(typeLen + idLen));

    var serviceId = MemoryMarshal.AsBytes((ReadOnlySpan<sbyte>)ServiceId);
    serviceId.CopyTo(s.Slice(typeLen + idLen + locSizeLen));

    s.Slice(typeLen + idLen + locSizeLen + serviceId.Length)[0] = (byte)':';

    var rpcMethod = MemoryMarshal.AsBytes((ReadOnlySpan<sbyte>)RpcMethod);
    rpcMethod.CopyTo(s.Slice(typeLen + idLen + locSizeLen + serviceId.Length + 1));

    {
      Raw.TryGetMemoryOwner(out var owner);
      if (!Raw.TryGetMemory(out var mem))
        throw new NotImplementedException();
      var slice = mem.Slice(checked((int)HeaderSize), (int)BodySize);
      var sendTask = ctx.SendAsync(StreamOnly, header, slice);
      return owner is not null
        ? sendTask.ContinueWith(_ => owner.Dispose())
        : sendTask;
    }
  }
}
