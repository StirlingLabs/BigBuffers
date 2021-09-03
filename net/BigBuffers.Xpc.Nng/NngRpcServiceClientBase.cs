#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using nng;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  public abstract class NngRpcServiceClientBase
  {
    private readonly TextWriter _logger;
    protected IPairSocket Pair { get; }
    protected IAPIFactory<INngMsg> Factory { get; }
    public abstract ReadOnlySpan<byte> ServiceId { get; }

    private readonly ConcurrentDictionary<long, AsyncProducerConsumerCollection<INngMsg>> _outstanding = new();

    protected NngRpcServiceClientBase(IPairSocket pair, IAPIFactory<INngMsg> factory, TextWriter logger)
    {
      _logger = logger;
      Pair = pair;
      Factory = factory;
    }

    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    /// <summary>
    /// Listens for replies to sent messages and dispatches them to outstanding calls.
    /// </summary>
    public Task RunAsync(CancellationToken cancellationToken)
      => Task.Run(async () => {

        static void ParseReply(INngMsg msg, out long id, out NngMessageType type)
        {
          var req = msg.ParseReply();
          id = req.Id();
          type = req.Type();
        }

        try
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: starting parse reply loop");

          while (!cancellationToken.IsCancellationRequested)
          {
            NngResult<INngMsg> received;
            using (var ctx = Pair.CreateAsyncContext(Factory).Unwrap())
              received = await ctx.Receive(cancellationToken);

            if (cancellationToken.IsCancellationRequested) break;

            INngMsg reply;
            try
            {
              reply = received.Unwrap();
            }
            catch (NngException ex)
            {
              // TODO: failed to receive any message
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: {ex.GetType().FullName}: {ex.Message}");
              continue;
            }
            ParseReply(reply, out var id, out var rt);
#if NET5_0_OR_GREATER
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} #{id} T{Task.CurrentId}: received reply {rt}: 0x{Convert.ToHexString(reply.AsSpan())}");
#endif
            if (!_outstanding.TryGetValue(id, out var c))
            {
              // TODO: no outstanding request
              continue;
            }

            if (c.IsAddingCompleted)
            {
              // TODO: already received final message
              continue;
            }

            if (c.IsCompleted)
            {
              // TODO: outstanding request already closed
              continue;
            }

            if (!c.TryAdd(reply))
            {
              // TODO: what kind of concurrent collection rejects an add? some sort of set?
              continue;
            }

            if ((rt & NngMessageType.Final) != 0)
            {
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: marking replies complete");
              c.CompleteAdding();
            }
          }
        }
        finally
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: completing parse reply loop");
          foreach (var outstanding in _outstanding)
          {
            var id = outstanding.Key;
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} #{id} T{Task.CurrentId}: cleaning up replies");
            var replies = outstanding.Value;
            replies.CompleteAdding();
            replies.Clear(); // so the collection becomes complete
            _outstanding.TryRemove(outstanding.Key, out var _);
          }
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: completed parse reply loop");
        }
      }, cancellationToken);

    protected IAsyncEnumerable<INngMsg> GetReplies(long msgId)
    {
      var messages = _outstanding.GetOrAdd(msgId, _ => new(new ConcurrentQueue<INngMsg>()));

      return messages.GetConsumer();
    }

    protected void ClearReplies(long msgId)
    {
      if (_outstanding.TryRemove(msgId, out var q))
        q.Clear();
    }

    protected abstract ReadOnlySpan<byte> ResolveMethodSignature<TMethodEnum>(TMethodEnum method) where TMethodEnum : struct, Enum;

    protected async Task<TReply> UnaryRequest<TMethodEnum, TReply, TRequest>(TMethodEnum method, TRequest item,
      CancellationToken cancellationToken)
      where TMethodEnum : struct, Enum
      where TRequest : struct, IBigBufferTable
      where TReply : struct, IBigBufferTable
    {
      long msgId;

      IAsyncEnumerable<INngMsg> replies;
      using (var ctx = Pair.CreateAsyncContext(Factory).Unwrap())
      {
        var request = Factory.CreateRequest(ServiceId, ResolveMethodSignature(method), item, out msgId);

#if NET5_0_OR_GREATER
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: sending request {method} message: {Convert.ToHexString(request.AsSpan())}");
#endif

        (await ctx.Send(request)).Unwrap();

        replies = GetReplies(msgId);

        ctx.Aio.Wait();
      }

      try
      {
        await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
        {
          if (HandleReply(replyMsg, out TReply result, true))
            return result;
        }
      }
      finally
      {
        ClearReplies(msgId);
      }

      cancellationToken.ThrowIfCancellationRequested();
      throw new("The request completed with no reply.");
    }

    protected async Task<TReply> ClientStreamingRequest<TMethodEnum, TReply, TRequest>(TMethodEnum method, IAsyncEnumerable<TRequest> items,
      CancellationToken cancellationToken)
      where TMethodEnum : struct, Enum
      where TRequest : struct, IBigBufferTable
      where TReply : struct, IBigBufferTable
    {
      var msgId = SharedCounters.ReadAndIncrementMessageCount();
      IAsyncEnumerable<INngMsg>? replies = null;

      using (var ctx = Pair.CreateAsyncContext(Factory).Unwrap())
      {
#if DEBUG
        var part = 1;
#endif
        INngMsg request;
        await foreach (var item in items.WithCancellation(cancellationToken))
        {
          request = Factory.CreateStreamingRequest(ServiceId, ResolveMethodSignature(method), item, msgId);

#if DEBUG && NET5_0_OR_GREATER
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: sending request {method} part {part}: {Convert.ToHexString(request.AsSpan())}");
#endif

          (await ctx.Send(request)).Unwrap();

          replies ??= GetReplies(msgId);

          ctx.Aio.Wait();

#if DEBUG
          ++part;
#endif
        }

        request = Factory.CreateNoMoreFollowsControlRequest(ServiceId, ResolveMethodSignature(method), msgId);

#if NET5_0_OR_GREATER
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: sending final control request to close {method}: {Convert.ToHexString(request.AsSpan())}");
#endif

        (await ctx.Send(request)).Unwrap();

        replies ??= GetReplies(msgId);

        //ctx.Aio.Wait();
      }

      try
      {
        await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
        {
          if (HandleReply(replyMsg, out TReply result, true))
            return result;
        }
      }
      finally
      {
        ClearReplies(msgId);
      }

      cancellationToken.ThrowIfCancellationRequested();
      throw new("The request completed with no reply.");
    }

    protected async IAsyncEnumerable<TReply> ServerStreamingRequest<TMethodEnum, TReply, TRequest>(TMethodEnum method, TRequest item,
      [EnumeratorCancellation] CancellationToken cancellationToken)
      where TMethodEnum : struct, Enum
      where TRequest : struct, IBigBufferTable
      where TReply : struct, IBigBufferTable
    {

      long msgId;

      IAsyncEnumerable<INngMsg> replies;
      using (var ctx = Pair.CreateAsyncContext(Factory).Unwrap())
      {
        var request = Factory.CreateRequest(ServiceId, ResolveMethodSignature(method), item, out msgId);

#if NET5_0_OR_GREATER
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: sending request {method}: {Convert.ToHexString(request.AsSpan())}");
#endif

        (await ctx.Send(request)).Unwrap();

        replies = GetReplies(msgId);

        //ctx.Aio.Wait();
      }

      try
      {
        await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
        {
          if (HandleReply(replyMsg, out TReply result, false))
            yield return result;
        }
      }
      finally
      {
        ClearReplies(msgId);
      }
    }

    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    protected async IAsyncEnumerable<TReply> StreamingRequest<TMethodEnum, TReply, TRequest>(TMethodEnum method,
      IAsyncEnumerable<TRequest> items,
      [EnumeratorCancellation] CancellationToken cancellationToken)
      where TMethodEnum : struct, Enum
      where TRequest : struct, IBigBufferTable
      where TReply : struct, IBigBufferTable
    {

      var msgId = SharedCounters.ReadAndIncrementMessageCount();

      using var sentFirstMessage = new SemaphoreSlim(0, 1);

      var replies = GetReplies(msgId);

      using var sending = Task.Run(async () => {
        using var ctx = Pair.CreateAsyncContext(Factory).Unwrap();
        INngMsg request;
#if DEBUG
        var part = 1;
#endif
        await foreach (var item in items.WithCancellation(cancellationToken))
        {

          request = Factory.CreateStreamingRequest(ServiceId, ResolveMethodSignature(method), item, msgId);

#if DEBUG && NET5_0_OR_GREATER
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: sending request {method} part {part}: {Convert.ToHexString(request.AsSpan())}");
#endif

          (await ctx.Send(request)).Unwrap();

          ctx.Aio.Wait();
#if DEBUG
          ++part;
#endif
        }
        request = Factory.CreateNoMoreFollowsControlRequest(ServiceId, ResolveMethodSignature(method), msgId);

#if NET5_0_OR_GREATER
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: sending final control request to close {method}: {Convert.ToHexString(request.AsSpan())}");
#endif

        (await ctx.Send(request)).Unwrap();

        //ctx.Aio.Wait();
      }, cancellationToken);

      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: getting replies to {method}");

      try
      {
        var i = 0;
        await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
        {
          ++i;
          if (HandleReply(replyMsg, out TReply result, false))
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: handled reply {i} to {method}");

            yield return result;
          }
          else
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: did not handle reply {i} to {method}");
          }
        }

      }
      finally
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: finalizing {method}");
        ClearReplies(msgId);
        if (!sending.IsCompleted)
        {
          if (!cancellationToken.IsCancellationRequested)
          {
            sending.Wait(cancellationToken);
          }
          else
          {
            sending.Wait(100);
            Debug.Assert(sending.IsCompleted);
          }
        }
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: finalized {method}");

      }
    }

    private bool HandleReply<TReply>(INngMsg replyMsg, out TReply result, bool unary) where TReply : struct, IBigBufferTable
    {
      var reply = replyMsg.ParseReply();

      var msgType = reply.Type();

#if NET5_0_OR_GREATER
      _logger?.WriteLine(
        $"[{TimeStamp:F3}] {GetType().Name} #{reply.Id()} T{Task.CurrentId}: handling reply: {Convert.ToHexString(replyMsg.AsSpan())}");
#endif

      if ((msgType & NngMessageType.Control) != 0)
      {
        if ((msgType & NngMessageType.Final) == 0)
        {
          if (unary) // non-final control request reply to unary method
            throw new("Unknown final control request.")
              { Data = { [typeof(INngMsg)] = replyMsg } };
          throw new("Unknown non-final control request.")
            { Data = { [typeof(INngMsg)] = replyMsg } };
        }

        if (reply.Body().Length == 0)
        {
          // final control request reply with no body to non-unary method is the end of the messages
          Unsafe.SkipInit(out result);
          return false;
        }

        if (unary) // final control request to unary method is an error
          throw new("The service was unable to handle the request.")
            { Data = { [typeof(INngMsg)] = replyMsg } };

        // non-final control request to non-unary method is not implemented
        throw new("Unknown non-final control request.")
          { Data = { [typeof(INngMsg)] = replyMsg } };
      }

      if (unary)
        if ((msgType & NngMessageType.Final) == 0)
          throw new("The service had more than one result.")
            { Data = { [typeof(INngMsg)] = replyMsg } };

      var body = reply.Body();
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
  }
}
