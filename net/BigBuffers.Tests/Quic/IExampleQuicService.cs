using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Generated;

namespace BigBuffers.Tests;

public interface IExampleQuicService {

  Task<Message> Unary(Message m, CancellationToken ct = default);

  Task<Message> ClientStreaming(ChannelReader<Message> msgs, CancellationToken ct = default);

  Task ServerStreaming(Message m, ChannelWriter<Message> writer, CancellationToken ct = default);

  Task BidirectionalStreaming(ChannelReader<Message> msgs, ChannelWriter<Message> writer, CancellationToken ct = default);

}