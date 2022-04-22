using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public class ReplyMessage : IMessage {

  private MessageContext? _context;

  public IQuicRpcServiceContext? Context {
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

  public ref byte GetPinnableReference()
    => ref Raw.GetPinnableReference();

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public ReplyMessage(IQuicRpcServiceContext serviceCtx, MessageType type, BigMemory<byte> raw, long id = 0) {
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
  public ReplyMessage(BigMemory<byte> raw, bool withoutHeader, bool withoutId = false) {
    _context = null;

    Raw = raw;
    StreamOnly = false;

    if (withoutHeader) {
      Id = -1;
      Type = unchecked((MessageType)(-1));
      HeaderSize = 0;
      return;
    }

    var span = raw.BigSpan;
    var typeSize = (uint)VarIntSqlite4.GetDecodedLength(span[0u]);
    Type = (MessageType)VarIntSqlite4.Decode((ReadOnlySpan<byte>)span.Slice(0, typeSize));


    if (withoutId)
      HeaderSize = typeSize;
    else {
      var idSize = (uint)VarIntSqlite4.GetDecodedLength(span[typeSize]);
      Id = (long)VarIntSqlite4.Decode((ReadOnlySpan<byte>)span.Slice(typeSize, idSize));
      HeaderSize = typeSize + idSize;
    }
  }

  public Task SendAsync() {
    var ctx = _context ?? throw new InvalidOperationException("Message is not able to be sent.");

    if (ctx.AlreadySent) throw new InvalidOperationException("Already sent.");

    // id, type
    Debug.Assert((Type & MessageType.Reply) != 0);
    var typeLen = VarIntSqlite4.GetEncodedLength(checked((ulong)Type));
    var idLen = VarIntSqlite4.GetEncodedLength(checked((ulong)Id));
    var headerSize = idLen + typeLen;
    var header = new Memory<byte>(new byte[headerSize]);

    var s = header.Span;
    VarIntSqlite4.Encode((ulong)Type, s);
    VarIntSqlite4.Encode((ulong)Id, s.Slice(idLen));
    {
      if (!Raw.TryGetMemory(out var mem))
        throw new NotImplementedException();

      var slice = mem.Slice(checked((int)HeaderSize), (int)BodySize);

      if (!Raw.TryGetMemoryOwner(out var owner) || owner is null)
        return ctx.SendAsync(StreamOnly, header, slice);

      return ctx.SendAsync(StreamOnly, header, slice)
        .ContinueWith(_ => owner.Dispose());
    }
  }

}
