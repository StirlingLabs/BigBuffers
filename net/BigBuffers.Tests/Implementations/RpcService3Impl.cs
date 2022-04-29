using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Generated;

namespace BigBuffers.Tests;

public sealed class RpcService3Impl : IRpcService3 {
  
  public async Task<Message> Unary(Message m, CancellationToken ct = default) {
    var bb = new BigBufferBuilder();
    Message.StartMessage(bb);
    Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
    Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
    Message.AddBodyType(bb, MessageBody.Empty);
    var reply = Message.EndMessage(bb).Resolve(bb);
    Empty.StartEmpty(bb);
    e.Fill(Empty.EndEmpty(bb));
    subj.Fill("RE: " + m.Subject);
    return reply;
  }

  public async Task<Message> ClientStreaming(ChannelReader<Message> msgs, CancellationToken ct = default) {
    var subjects = new List<string>();
    await foreach (var msg in msgs.AsConsumingAsyncEnumerable(ct))
      subjects.Add(msg.Subject);

    var bb = new BigBufferBuilder();
    Message.StartMessage(bb);
    Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
    Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
    Message.AddBodyType(bb, MessageBody.Empty);
    var reply = Message.EndMessage(bb).Resolve(bb);
    Empty.StartEmpty(bb);
    e.Fill(Empty.EndEmpty(bb));
    subj.Fill($"RE: {string.Join(", ", subjects)}");
    return reply;
  }

  public async Task ServerStreaming(Message m, ChannelWriter<Message> writer, CancellationToken ct = default) {
    try {
      var subject = m.Subject;

      {
        var bb = new BigBufferBuilder();
        Message.StartMessage(bb);
        Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
        Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
        Message.AddBodyType(bb, MessageBody.Empty);
        var reply = Message.EndMessage(bb).Resolve(bb);
        Empty.StartEmpty(bb);
        e.Fill(Empty.EndEmpty(bb));
        subj.Fill($"RE: {subject}");
          
        await Task.Delay(1, ct);
          
        await writer.WriteAsync(reply, ct);
      }

      {
        var bb = new BigBufferBuilder();
        Message.StartMessage(bb);
        Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
        Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
        Message.AddBodyType(bb, MessageBody.Empty);
        var reply = Message.EndMessage(bb).Resolve(bb);
        Empty.StartEmpty(bb);
        e.Fill(Empty.EndEmpty(bb));
        subj.Fill($"RE Ctd.: {subject}");

        await Task.Delay(1, ct);

        await writer.WriteAsync(reply, ct);
      }
    }
    finally {
      writer.Complete();
    }
  }

  public async Task BidirectionalStreaming(ChannelReader<Message> msgs, ChannelWriter<Message> writer, CancellationToken ct = default) {
    try {
      await foreach (var msg in msgs.AsConsumingAsyncEnumerable(ct)) {
        var subject = msg.Subject;

        var bb = new BigBufferBuilder();
        Message.StartMessage(bb);
        Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subj));
        Message.AddBody(bb, bb.MarkOffsetPlaceholder(out Placeholder<Empty> e).Value);
        Message.AddBodyType(bb, MessageBody.Empty);
        var reply = Message.EndMessage(bb).Resolve(bb);
        Empty.StartEmpty(bb);
        e.Fill(Empty.EndEmpty(bb));
        subj.Fill($"RE: {subject}");

        await Task.Delay(1, ct);

        await writer.WriteAsync(reply, ct);
      }
    }
    finally {
      writer.Complete();
    }
  }

}
