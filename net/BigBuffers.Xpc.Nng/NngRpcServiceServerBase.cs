#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using nng;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Nng
{
  [PublicAPI]
  public abstract class NngRpcServiceServerBase
  {
    protected readonly TextWriter? _logger;
    protected IPairSocket Pair { get; }
    protected IAPIFactory<INngMsg> Factory { get; }
    protected abstract ReadOnlySpan<byte> Utf8ServiceId { get; }

    private ConcurrentDictionary<(int, long), AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)>> _clientMsgStreams = new();
    private ConcurrentDictionary<(int, long), IAsyncDisposable> _serverMsgStreams = new();

    private static readonly Regex RxSplitPascalCase = new("(?<=[a-z])([A-Z])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly byte[]
      Utf8ErrorBadRequest = Encoding.UTF8.GetBytes("Bad Request"),
      Utf8ErrorUnauthorized = Encoding.UTF8.GetBytes("Unauthorized"),
      Utf8ErrorNotFound = Encoding.UTF8.GetBytes("Not Found"),
      Utf8ErrorRequestTimeout = Encoding.UTF8.GetBytes("Request Timeout"),
      Utf8ErrorGone = Encoding.UTF8.GetBytes("Gone"),
      Utf8ErrorNotImplemented = Encoding.UTF8.GetBytes("Not Implemented"),
      Utf8ErrorInternalServerError = Encoding.UTF8.GetBytes("Internal Server Error");

    [Discardable]
    protected static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    protected NngRpcServiceServerBase(IPairSocket pair, IAPIFactory<INngMsg> factory, TextWriter? logger = null)
    {
      _logger = logger;
      Pair = pair;
      Factory = factory;
    }

    protected TMethodEnum ParseRequest<TMethodEnum>(INngMsg nngMsg, out ByteBuffer bb, out long msgId, out NngMessageType msgType)
      where TMethodEnum : struct, Enum
    {
      var req = nngMsg.ParseRequest();
      msgId = req.Id();
      msgType = req.Type();
      var method = SelectMethod<TMethodEnum>(req);
      var body = req.Body();
      if (body.Length > 0)
        bb = new(body);
      else
        bb = new(0);
      return method;
    }

    protected TMethodEnum SelectMethod<TMethodEnum>(in NngRequestMessage req) where TMethodEnum : struct, Enum
    {
      var serviceName = req.ServiceId();

      if (!serviceName.SequenceEqual(Utf8ServiceId))
        return default;

      var procName = req.ProcedureName();

      var methods = (TMethodEnum[])typeof(TMethodEnum).GetEnumValues();

      foreach (var method in methods)
      {
        if (Unsafe.As<TMethodEnum, nint>(ref Unsafe.AsRef(method)) == 0)
          continue;

        if (procName.SequenceEqual(ResolveMethodSignature(method)))
          return method;
      }

      return default;
    }

    protected Task RunAsync<TMethodEnum>(CancellationToken cancellationToken) where TMethodEnum : struct, Enum
    {
      // used as a concurrent set
      ConcurrentDictionary<Task, _> dispatched = new();

      async Task DispatcherLoop()
      {
        try
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}: dispatcher loop started");
          while (!cancellationToken.IsCancellationRequested)
          {
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}: listening for new request");
            INngMsg request;
            using (var ctx = Pair.CreateAsyncContext(Factory).Unwrap())
              request = (await ctx.Receive(cancellationToken)).Unwrap();

            if (cancellationToken.IsCancellationRequested) break;

#if NET5_0_OR_GREATER
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}: received request: 0x{Convert.ToHexString(request.AsSpan())}");
#endif
            var sync = new SemaphoreSlim(0, 1);
            var runner = Task.Run(async () => {
              _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatched request started");
              await Dispatch<TMethodEnum>(Pair.Id, request, sync, cancellationToken);
              _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatched request completed");
            }, cancellationToken);
            var added = dispatched.TryAdd(runner, default);

            Debug.Assert(added);

            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: synchronizing with dispatch task T{runner.Id}");

            try
            {
              await sync.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
              Debug.Assert(sync.CurrentCount == 0);
              if (cancellationToken.IsCancellationRequested) break;
            }

            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: synchronized");

            // sweep outstanding tasks to prevent leakage over long runtime
            foreach (var kvp in dispatched)
            {
              if (!kvp.Key.IsCompleted)
                continue;

              dispatched.TryRemove(kvp.Key, out var _);
              _logger?.WriteLine(
                $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: collected completed dispatch request T{kvp.Key.Id}");
            }
          }
        }
        finally
        {

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatcher loop ending, finished dispatching, waiting on outstanding dispatches");
          // wait on any remaining outstanding tasks
          await Task.WhenAll(dispatched.Keys);

          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} T{Task.CurrentId}: dispatcher loop ended, no more outstanding dispatches");

        }
      }

      return Task.Run(DispatcherLoop, cancellationToken);
    }


#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ReadOnlySpan<T> CreateReadOnlySpan<T>(ref T item, int length)
      => new(Unsafe.AsPointer(ref item), 1);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteErrorCode(INngMsg response, long errCode)
    {
#if NETSTANDARD2_0
      response.Append(MemoryMarshal.AsBytes(CreateReadOnlySpan(ref errCode, 1)));
#else
      response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteNullByte(INngMsg response)
    {
      byte nullByte = 0;
#if NETSTANDARD2_0
      response.Append(MemoryMarshal.AsBytes(CreateReadOnlySpan(ref nullByte, 1)));
#else
      response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref nullByte, 1)));
#endif
    }

    protected class NngMsgStreamWriter<T> : ChannelWriter<T>, IAsyncDisposable, IDisposable
      where T : struct, IBigBufferEntity
    {
      private readonly TextWriter? _logger;
      protected NngRpcServiceServerBase Service { get; }

      protected long MessageId { get; }

      protected IPairSocket Pair => Service.Pair;

      protected IAPIFactory<INngMsg> Factory => Service.Factory;

      private Task? _active;

      private bool _completedAdding;

      private bool _disposed;

      public NngMsgStreamWriter(NngRpcServiceServerBase service, long msgId, TextWriter? logger = null)
      {
        _logger = logger;
        Service = service;
        MessageId = msgId;
      }

      public override bool TryWrite(T item)
      {
        if (_disposed) return false;
        if (_completedAdding) return false;
        if (_active is not null)
        {
          if (_active.IsFaulted)
            throw _active.Exception!;
          if (_active.IsCanceled)
            throw new TaskCanceledException(_active);
          if (!_active.IsCompleted)
            return false;
        }
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: sending {typeof(T).Name} stream message");
        var ctx = Pair.CreateAsyncContext(Factory).Unwrap();
        _active = ctx.Send(Factory.CreateReply(MessageId, item, NngMessageType.Normal))
          .ContinueWith(async result => {
            var dontThrow = false;
            if (!result.IsFaulted && !result.IsCanceled)
            {
              dontThrow = true;
              if ((await result).IsOk())
                ctx.Aio.Wait();
            }
            ctx.Dispose();
            if (dontThrow)
              return;
            if (result.IsFaulted)
              throw result.Exception!;
            if (result.IsCanceled)
              throw new TaskCanceledException(result);
          });
        return true;
      }

      public override async ValueTask<bool> WaitToWriteAsync(CancellationToken cancellationToken = default)
      {
        if (_disposed) return false;

        if (_completedAdding) return false;

        if (_active is null)
          return true;

        await _active;

        if (_active.IsFaulted)
          throw _active.Exception!;

        if (_active.IsCanceled)
          throw new TaskCanceledException(_active);

        return true;
      }


      public override bool TryComplete(Exception? error = null)
      {
        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: completing");
        _completedAdding = true;
        return true;
      }

      private async Task CloseAsync()
      {
        if (_active is not null)
          await _active;

        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: trying to close");
        if (!Pair.IsValid())
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: pair was invalid");
          return;
        }

        using var ctx = Pair.CreateAsyncContext(Factory).Unwrap();
        var final = Factory.CreateControlReply(MessageId);
#if NET5_0_OR_GREATER
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: sending {typeof(T).Name} final control stream message: 0x{Convert.ToHexString(final.AsSpan())}");
#endif
        await ctx.Send(final);

        //ctx.Aio.Wait();
      }

      private void Close()
        => CloseAsync().GetAwaiter().GetResult();

      public async ValueTask DisposeAsync()
      {
        if (_disposed) return;
        _disposed = true;
        await CloseAsync();
        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: disposing async");

        if (_active is null) return;

        await _active;
        _active.Dispose();
        _active = null;
      }

      public void Dispose()
      {
        if (_disposed) return;
        _disposed = true;
        Close();
        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{MessageId} T{Task.CurrentId}: disposing");

        if (_active is null) return;

        _active.ConfigureAwait(true).GetAwaiter().GetResult();
        _active.Dispose();
        _active = null;

      }
    }

    public class EntityNngMsgChannelReader<T> : ChannelReader<T> where T : struct, IBigBufferEntity
    {
      private readonly AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> _collection;
      private readonly TextWriter? _logger;

      public EntityNngMsgChannelReader(AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> collection, TextWriter? logger = null)
      {
        _collection = collection;
        _logger = logger;
      }


#if NETSTANDARD
      private readonly TaskCompletionSource<bool> _tcs = new();
#else
      private readonly TaskCompletionSource _tcs = new();
#endif

      public override bool TryRead(out T item)
      {
        Unsafe.SkipInit(out item);
        if (Completion.IsCompleted)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, messages previously completed");
          return false;
        }

        if (!_collection.TryTake(out var msg))
        {

          // ReSharper disable once InvertIf
          if (_collection.IsCompleted)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, messages completed");
#if NETSTANDARD
            _tcs.SetResult(true);
#else
            _tcs.SetResult();
#endif
            return false;
          }

          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to read entity, no messages yet");
          return false;
        }

        item = new() { Model = new(msg.Item2, 0) };
        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> #{msg.Item1.Id()} T{Task.CurrentId}: read entity");
        return true;
      }

      public override async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
      {
        if (Completion.IsCompleted)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: failed to wait, previously completed");
          return false;
        }

        if (!_collection.IsCompleted)
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting to read");
          await _collection.WaitForAvailableAsync(false, cancellationToken);
          if (!_collection.IsCompleted)
          {
            _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting completed");
            return true;
          }

          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: waiting failed");
        }

        _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name}<{typeof(T).Name}> T{Task.CurrentId}: collection was completed");

#if NETSTANDARD
        _tcs.SetResult(true);
#else
        _tcs.SetResult();
#endif

        return false;
      }

      public override Task Completion => _tcs.Task;
    }

    protected async Task Dispatch<TMethodEnum>(int pairId, INngMsg sourceMsg, SemaphoreSlim sync, CancellationToken cancellationToken)
      where TMethodEnum : struct, Enum
    {
      async Task SendUnaryReply(INngMsg? reply)
      {
        reply ??= OnUnhandledMessage(sourceMsg, cancellationToken);

        // still null?
        if (reply is null)
        {
          reply = Factory.CreateMessage();
          reply.Append(sourceMsg.AsSpan().Slice(0, 8));
          reply.Append(MemoryMarshal.AsBytes(stackalloc[] { NngMessageType.FinalControl }));
          // body
          WriteErrorCode(reply, 404);
          using var ctx = Pair.CreateAsyncContext(Factory).Unwrap();
          await ctx.Send(reply);
          ctx.Aio.Wait();
        }
        else if (reply is not NngMsgDoNotSend)
        {
          using var ctx = Pair.CreateAsyncContext(Factory).Unwrap();
          await ctx.Send(reply);
          ctx.Aio.Wait();
        }
      }

      var method = ParseRequest<TMethodEnum>(sourceMsg, out var bb, out var msgId, out var msgType);

      var methodType = ResolveMethodType(method);

      async Task Unary()
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} dispatching to unary {method} implementation");
        var dispatch = DispatchUnary(method, msgId, bb, cancellationToken);

        sync.Release();

        var reply = await HandleExceptions(dispatch, msgId, cancellationToken);

        await SendUnaryReply(reply);
      }

      async Task ClientStreaming()
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: dispatched client streaming request is {method}");

        var isNewStream = false;
        var c = _clientMsgStreams.GetOrAdd((pairId, msgId), _ => {
          isNewStream = true;
          return new();
        });

        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: dispatched client streaming request is {method} {msgType} and {(isNewStream ? "is" : "isn't")} a new stream");

        if ((msgType & NngMessageType.Control) == 0)
        {
          var added = c.TryAdd((sourceMsg, bb));
          Debug.Assert(added);

          if (!added)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: message was not added as the stream was already closed!");
          }
        }
        else
        {
          // TODO: handle control messages
        }

        if ((msgType & NngMessageType.Final) != 0)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: message was final so the stream is being closed");

          c.CompleteAdding();
        }

        if (!isNewStream)
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} synchronizing");
          sync.Release();
          return;
        }

        // long await
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} dispatching to client streaming {method} implementation");
        var dispatch = DispatchClientStreaming(method, msgId, c, cancellationToken);

        sync.Release();

        var reply = await HandleExceptions(dispatch, msgId, cancellationToken);

        await SendUnaryReply(reply);

        _clientMsgStreams.TryRemove((pairId, msgId), out var _);
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: marking messages complete due to implementation completed");
        c.CompleteAdding();
        c.Clear();

      }

      async Task ServerStreaming()
      {
        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: dispatched server streaming request is {method}");

        // NOTE: does not wait on the task to complete
        await Task.Factory.StartNew(async () => {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} started streaming");
          var t = DispatchServerStreaming(method, msgId, bb, cancellationToken);
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} synchronizing");
          sync.Release();
          await t;
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} finished streaming");
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
      }

      async Task Streaming()
      {

        var isNewStream = false;
        var c = _clientMsgStreams.GetOrAdd((pairId, msgId), _ => {
          isNewStream = true;
          return new();
        });

        _logger?.WriteLine(
          $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: dispatched bidirectional streaming request is {method} {msgType} and {(isNewStream ? "is" : "isn't")} a new stream");

        if ((msgType & NngMessageType.Control) == 0)
        {
          var added = c.TryAdd((sourceMsg, bb));
          Debug.Assert(added);

          if (!added)
          {
            _logger?.WriteLine(
              $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: message was not added as the stream was already closed!");
          }
        }
        else
        {
          // TODO: handle control messages
        }

        if ((msgType & NngMessageType.Final) != 0)
        {
          _logger?.WriteLine(
            $"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: message was final so the stream is being closed");

          c.CompleteAdding();
        }

        if (!isNewStream)
        {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} synchronizing");
          sync.Release();
          return;
        }

        // NOTE: does not wait on the task to complete
        await Task.Factory.StartNew(async () => {
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} started streaming");
          var t = DispatchStreaming(method, msgId, c, cancellationToken);
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} synchronizing");
          sync.Release();
          await t;
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} finished streaming");
          _clientMsgStreams.TryRemove((pairId, msgId), out var _);
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: marking stream complete");
          c.CompleteAdding();
          Debug.Assert(c.IsAddingCompleted);
          c.Clear();
          Debug.Assert(c.IsCompleted);
          _logger?.WriteLine($"[{TimeStamp:F3}] {GetType().Name} #{msgId} T{Task.CurrentId}: {method} cleaned up");
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
      }

      if ((msgType & NngMessageType.Continuation) != 0)
      {
        if (_clientMsgStreams.TryGetValue((pairId, msgId), out var c))
        {
          if (!c.TryAdd((sourceMsg, bb)))
          {
            // collection is already complete or completing
            // either the method has already ended or aborted
            Debug.Assert(c.IsAddingCompleted);
            GoneReply(msgId);
            return;
          }

          if ((msgType & NngMessageType.Final) != 0)
            c.CompleteAdding();
          return;
        }

        // the collection was not found, so clean up has
        // already been run or it was not created       
        NotFoundReply(msgId);
        return;
      }

      switch (methodType)
      {
        // @formatter:off
        case RpcMethodType.Unary: await Unary(); break;
        case RpcMethodType.ClientStreaming: await ClientStreaming(); break;
        case RpcMethodType.ServerStreaming: await ServerStreaming(); break;
        case RpcMethodType.BidirectionalStreaming: await Streaming(); break;
        // @formatter:on
        default:
          sync.Release();
          throw new NotImplementedException();
      }
    }
    protected async Task<INngMsg?> HandleExceptions(Func<Task<INngMsg?>> fn, long msgId, CancellationToken cancellationToken)
      => await HandleExceptions(fn(), msgId, cancellationToken);

    protected async Task<INngMsg?> HandleExceptions(Task<INngMsg?> task, long msgId, CancellationToken cancellationToken)
    {
      try
      {
        return await task;
      }
      catch (UnauthorizedAccessException ex)
      {
        return UnauthorizedReply(msgId, ex);
      }
      catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
      {
        return TimedOutReply(msgId, ex);
      }
      catch (NotImplementedException ex)
      {
        return NotImplementedExceptionReply(msgId, ex);
      }
#if NET5_0_OR_GREATER
      catch (HttpRequestException ex)
      {
        return UnhandledHttpExceptionReply(msgId, ex);
      }
#endif
      catch (Exception ex)
      {
        return UnhandledExceptionReply(msgId, ex);
      }
    }
    protected INngMsg FinalControlReply(long msgId, long errorCode, ReadOnlySpan<byte> message)
    {
      var response = Factory.CreateControlReply(msgId);
      WriteErrorCode(response, errorCode);
      response.Append(message);
      WriteNullByte(response);
      return response;
    }

    private void AddReplyExceptionMessage(INngMsg reply, Exception? ex = null)
    {
      if (ex is null) return;
      reply.Append(Encoding.UTF8.GetBytes($"{ex.GetType().Name}: {ex.Message}"));
      WriteNullByte(reply);
    }

    protected INngMsg BadRequestReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 400, Utf8ErrorBadRequest);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected INngMsg UnauthorizedReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 401, Utf8ErrorUnauthorized);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected INngMsg NotFoundReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 404, Utf8ErrorNotFound);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected INngMsg TimedOutReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 408, Utf8ErrorRequestTimeout);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected INngMsg GoneReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 410, Utf8ErrorGone);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected INngMsg NotImplementedExceptionReply(long msgId, Exception? ex = null)
    {
      var reply = FinalControlReply(msgId, 501, Utf8ErrorNotImplemented);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

#if NET5_0_OR_GREATER
    protected INngMsg UnhandledHttpExceptionReply(long msgId, HttpRequestException ex)
    {
      // ReSharper disable once ConstantNullCoalescingCondition
      ex ??= new("An exception was not provided.");
      var statusCode = ex.StatusCode ?? HttpStatusCode.InternalServerError;
      var message = RxSplitPascalCase.Replace(statusCode.ToString(), " $1");
      var reply = FinalControlReply(msgId, (long)statusCode, Encoding.UTF8.GetBytes(message));
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }
#endif
    protected INngMsg UnhandledExceptionReply(long msgId, Exception ex)
    {
      var reply = FinalControlReply(msgId, 500, Utf8ErrorInternalServerError);
#if DEBUG
      AddReplyExceptionMessage(reply, ex);
#endif
      return reply;
    }

    protected virtual Task<INngMsg?> DispatchUnary<TMethodEnum>(
      TMethodEnum method,
      long sourceMsgId,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) => Task.FromResult<INngMsg?>(null);

    protected virtual Task<INngMsg?> DispatchClientStreaming<TMethodEnum>(
      TMethodEnum method,
      long sourceMsgId,
      AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> reader,
      CancellationToken cancellationToken
    ) => Task.FromResult<INngMsg?>(null);

    protected virtual Task DispatchServerStreaming<TMethodEnum>(
      TMethodEnum method,
      long sourceMsgId,
      ByteBuffer sourceByteBuffer,
      CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected virtual Task DispatchStreaming<TMethodEnum>(
      TMethodEnum method,
      long sourceMsgId,
      AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> reader,
      CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected abstract ReadOnlySpan<byte> ResolveMethodSignature<TMethodEnum>(TMethodEnum method) where TMethodEnum : Enum;

    protected abstract RpcMethodType ResolveMethodType<TMethodEnum>(TMethodEnum method) where TMethodEnum : Enum;

    /// <summary>
    /// Listens for and processes incoming procedure calls into method invocations. 
    /// </summary>
    /// <remarks>
    /// Should implement by invoking <see cref="RunAsync{TMethodEnum}"/>.
    /// </remarks>
    public abstract Task RunAsync(CancellationToken cancellationToken);

    protected virtual INngMsg? OnUnhandledMessage(INngMsg msg, CancellationToken cancellationToken)
      => OnUnhandledMessage(cancellationToken);

    protected virtual INngMsg? OnUnhandledMessage(CancellationToken cancellationToken)
      => null;

    protected static T Track<T>(T disposable, ICollection<IAsyncDisposable> collection) where T : IAsyncDisposable
    {
      collection.Add(disposable);
      return disposable;
    }

    protected ChannelReader<T> WrapReader<T>(AsyncProducerConsumerCollection<(INngMsg, ByteBuffer)> r) where T : struct, IBigBufferEntity
      => new EntityNngMsgChannelReader<T>(r, _logger);
  }
}
