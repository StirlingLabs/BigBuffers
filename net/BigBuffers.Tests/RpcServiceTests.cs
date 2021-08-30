using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Generated;
using nng;
using NUnit.Framework;
using StirlingLabs.Utilities.Assertions;

namespace BigBuffers.Tests
{
  public class RpcServiceTests
  {
    private IAPIFactory<INngMsg> _factory;

    [SetUp]
    public void SetUp()
    {

      var path = Path.GetDirectoryName(typeof(RpcServiceTests).Assembly.Location);
      var ctx = new NngLoadContext(path);
      _factory = NngLoadContext.Init(ctx);
    }

    [Test]
    public async Task NngInProcPairSanityCheck()
    {
      const string url = "inproc://SanityCheck";
      var sanityCheckBytes = Encoding.UTF8.GetBytes("Sanity check");

      await Task.WhenAll(
        Task.Run(async () => {
          // server
          var pair = _factory.PairOpen().Unwrap();
          pair.Listen(url).Unwrap();

          var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();
          var msg = _factory.CreateMessage();
          msg.Append(sanityCheckBytes);
          await asyncCtx.Send(msg);
        }),
        Task.Run(async () => {
          // client
          var pair = _factory.PairOpen().Unwrap();
          pair.Dial(url).Unwrap();

          var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();
          var msg = (await asyncCtx.Receive(default)).Unwrap();

          msg.AsSpan().SequenceEqual(sanityCheckBytes).Should().BeTrue();
        })
      );
    }

    [Test]
    public async Task GeneralOperations()
    {
      const string url = "inproc://RpcService";
      var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      var completed = false;
      try
      {
        await Task.WhenAll(
          Task.Run(async () => {
            // server
            var pair = _factory.PairOpen().Unwrap();
            pair.Listen(url).Unwrap();

            //var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();

            var server = new RpcServiceNngServerImpl(pair, _factory);

            await server.RunAsync(cts.Token);
          }, cts.Token),
          Task.Run(async () => {
            // client
            var pair = _factory.PairOpen().Unwrap();
            await RetryOnThrowOrFail(() => pair.Dial(url), cts.Token);

            //var asyncCtx = pair.CreateAsyncContext(_factory).Unwrap();

            var client = new RpcServiceNng.Client(pair, _factory);

            var runner = client.RunAsync(cts.Token);

            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
              Message.AddBodyType(bb, MessageBody.TextMessage);
              var msg = Message.EndMessage(bb).Resolve(bb);
              TextMessage.StartTextMessage(bb);
              TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));
              var textContent = TextMessage.EndTextMessage(bb);
              body.Fill(textContent);
              subject.Fill("Hello World");
              content.Fill("Succeed");

              //msg.BodyAsTextMessage().Content.Should().Be("Succeed");

              var result = await client.Send(msg, cts.Token);
              result.Code.Should().Be(StatusCode.Success);
            }
            
            {
              var bb = new BigBufferBuilder();
              Message.StartMessage(bb);
              Message.AddSubject(bb, bb.MarkStringPlaceholder(out var subject));
              Message.AddBody(bb, bb.MarkOffsetPlaceholder<TextMessage>(out var body).Value);
              Message.AddBodyType(bb, MessageBody.TextMessage);
              var msg = Message.EndMessage(bb).Resolve(bb);
              TextMessage.StartTextMessage(bb);
              TextMessage.AddContent(bb, bb.MarkStringPlaceholder(out var content));  
              var textContent = TextMessage.EndTextMessage(bb);
              body.Fill(textContent);
              subject.Fill("Hello World");
              content.Fill("Fail");

              //msg.BodyAsTextMessage().Content.Should().Be("Fail");

              var result = await client.Send(msg, cts.Token);
              result.Code.Should().Be(StatusCode.Failure);
            }
            completed = true;
            cts.Cancel();

            await runner;
          }, cts.Token)
        );
      }
      catch (TaskCanceledException)
      {
        if (!completed) throw;
      }

      completed.Should().BeTrue();
    }

    private Task<NngResult<Unit>> RetryOnThrowOrFail(Func<NngResult<Unit>> func, CancellationToken cancellationToken)
      => RetryOnThrowOrFail(func, TimeSpan.FromSeconds(1), cancellationToken);

    private async Task<NngResult<Unit>> RetryOnThrowOrFail(Func<NngResult<Unit>> func, TimeSpan delaySeconds,
      CancellationToken cancellationToken)
    {
      while (!cancellationToken.IsCancellationRequested)
      {
        NngResult<Unit> result;
        result = func();
        if (result.IsOk())
          return result;
        await Task.Delay(delaySeconds);
      }

      cancellationToken.ThrowIfCancellationRequested();

      throw new NotImplementedException();
    }
  }
}
