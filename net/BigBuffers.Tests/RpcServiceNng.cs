#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Generated;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using nng;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Tests
{
  public static class SharedMessageCounter
  {
    internal static ulong Value;

    public static ulong ReadAndIncrement()
      => Interlocked.Increment(ref Value);
  }


  [Flags]
  public enum MessageType : ulong
  {
    Normal = 0,
    Control = 1,
    Final = 2,
    FinalControl = Control | Final
  }


  internal struct _ { }

  public readonly ref struct RequestMessage
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RequestMessage(INngMsg msg) : this(msg.AsSpan()) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RequestMessage(ReadOnlySpan<byte> raw) => Raw = raw;

    public readonly ReadOnlySpan<byte> Raw;
  }

  public readonly ref struct ReplyMessage
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReplyMessage(INngMsg msg) : this(msg.AsSpan()) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReplyMessage(ReadOnlySpan<byte> raw) => Raw = raw;

    public readonly ReadOnlySpan<byte> Raw;
  }

  public static class MessageExtensions
  {
    public static ref readonly ulong Id(in this RequestMessage req)
    {
      ref readonly var a = ref req.Raw[0];
      return ref Unsafe.As<byte, ulong>(ref Unsafe.AsRef(a));
    }
    public static ref readonly ulong Id(in this ReplyMessage rep)
    {
      ref readonly var start = ref rep.Raw[0];
      return ref Unsafe.As<byte, ulong>(ref Unsafe.AsRef(start));
    }
    public static ref readonly MessageType Type(in this RequestMessage req)
    {
      ref readonly var start = ref req.Raw[8];
      return ref Unsafe.As<byte, MessageType>(ref Unsafe.AsRef(start));
    }
    public static ref readonly MessageType Type(in this ReplyMessage rep)
    {
      ref readonly var start = ref rep.Raw[8];
      return ref Unsafe.As<byte, MessageType>(ref Unsafe.AsRef(start));
    }

    private static int FindByteOffset(ReadOnlySpan<byte> span, int start, byte value)
      => span.Slice(start).IndexOf(value);

    private static int GetNullTerminatedStringLength(ReadOnlySpan<byte> span, int start)
      => FindByteOffset(span, start, 0);

    private static ReadOnlySpan<byte> ReadNullTerminatedString(ReadOnlySpan<byte> span, int startIndex)
    {
      ref readonly var start = ref span[startIndex];
      return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(start),
        GetNullTerminatedStringLength(span, startIndex));
    }

    public static ReadOnlySpan<byte> Locator(in this RequestMessage req)
      => ReadNullTerminatedString(req.Raw, 16);

    public static ReadOnlySpan<byte> ServiceId(in this RequestMessage req)
    {
      var loc = req.Locator();
      var sepIndex = loc.IndexOf((byte)':');
      return sepIndex < 0 ? default : loc[..sepIndex];
    }
    public static ReadOnlySpan<byte> ProcedureName(in this RequestMessage req)
    {
      var loc = req.Locator();
      return loc[(1 + loc.IndexOf((byte)':'))..];
    }

    public static ReadOnlySpan<byte> Body(in this RequestMessage req)
    {
      var start = GetNullTerminatedStringLength(req.Raw, 16) + 16;
      const int align = 8;
      var alignBits = align - 1;
      var padding = (align - (start & alignBits)) & alignBits;
      return req.Raw[(start + padding)..];
    }

    public static ReadOnlySpan<byte> Body(in this ReplyMessage rep)
      => rep.Raw[16..];

    public static int PadToAlignment(this INngMsg msg, int align, byte paddingByte = 0)
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      static bool IsPow2(int value) => (value & (value - 1)) == 0 && value != 0;

      Debug.Assert(IsPow2(align));
      var alignBits = align - 1;
      var needed = (align - (msg.Length & alignBits)) & alignBits;
      var paddingByteSpan = MemoryMarshal.CreateReadOnlySpan(ref paddingByte, 1);
      for (var i = 0; i < needed; ++i)
        msg.Append(paddingByteSpan);
      return needed;
    }

    public static RequestMessage ParseRequest(this INngMsg msg) => new(msg);

    public static ReplyMessage ParseReply(this INngMsg msg) => new(msg);
  }

  public static class RpcServiceNng
  {
    private static readonly Type ServiceInterfaceType = typeof(IRpcService);
    private static readonly byte[] Utf8ServiceId = Encoding.UTF8.GetBytes(ServiceInterfaceType.Namespace + ".RpcService");
    private static readonly byte[] Utf8CallSend = Encoding.UTF8.GetBytes("Send");
    private static readonly byte[] Utf8CallReceive = Encoding.UTF8.GetBytes("Receive");
    private static readonly byte[]? NullByte = { 0 };

    private static ReadOnlySpan<byte> GetBytes<T>(T entity) where T : struct, IBigBufferEntity
      => entity.IsValid()
        ? (ReadOnlySpan<byte>)entity.Model.ByteBuffer.ToReadOnlySpan(0, entity.Model.ByteBuffer.LongLength)
        : throw new NotImplementedException();


    private static INngMsg CreateRequest<T>(this IMessageFactory<INngMsg> factory, ReadOnlySpan<byte> procName, T body, out ulong msgId,
      MessageType msgType = MessageType.Final)
      where T : struct, IBigBufferEntity
    {
      var msg = factory.CreateMessage();
      msgId = SharedMessageCounter.ReadAndIncrement();
      var msgIdSpan = MemoryMarshal.Cast<ulong, byte>(MemoryMarshal.CreateReadOnlySpan(ref msgId, 1));
      msg.Append(msgIdSpan);

      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType })); // normal message
      msg.Append(Utf8ServiceId);
      msg.Append(stackalloc[] { (byte)':' });
      msg.Append(procName);
      msg.Append(NullByte);
      msg.PadToAlignment(8);
      if (body.IsValid())
        msg.Append(GetBytes(body));
      return msg;
    }

    private static INngMsg CreateReply<T>(this IMessageFactory<INngMsg> factory, INngMsg sourceMsg, T body,
      MessageType msgType = MessageType.Final)
      where T : struct, IBigBufferEntity
    {
      var msg = factory.CreateMessage();
      var msgIdSpan = sourceMsg.AsSpan().Slice(0, 8);
      msg.Append(msgIdSpan);
      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      msg.Append(GetBytes(body));
      return msg;
    }


    private static INngMsg CreateControlReply(this IMessageFactory<INngMsg> factory, INngMsg sourceMsg,
      MessageType msgType = MessageType.FinalControl)
    {
      var msg = factory.CreateMessage();
      var msgIdSpan = sourceMsg.AsSpan().Slice(0, 8);
      msg.Append(msgIdSpan);
      msg.Append(MemoryMarshal.AsBytes(stackalloc[] { msgType }));
      return msg;
    }

    public enum Method
    {
      _ = 0,
      Send,
      Receive
    }

    public class Client : IRpcService
    {
      private readonly IPairSocket _pair;
      private readonly IAPIFactory<INngMsg> _factory;
      private readonly ConcurrentDictionary<ulong, AsyncProducerConsumerCollection<INngMsg>> _outstanding = new();

      public Client(IPairSocket pair, IAPIFactory<INngMsg> factory)
      {
        _pair = pair;
        _factory = factory;
      }

      /// <summary>
      /// Listens for replies to sent messages and dispatches them to outstanding calls.
      /// </summary>
      public Task RunAsync(CancellationToken cancellationToken)
        => Task.Run(async () => {
          while (!cancellationToken.IsCancellationRequested)
          {
            do
            {
              NngResult<INngMsg> received;
              using (var ctx = _pair.CreateAsyncContext(_factory).Unwrap())
                received = await ctx.Receive(cancellationToken);
              INngMsg msg;
              try
              {
                msg = received.Unwrap();
              }
              catch (NngException ex)
              {
                // TODO: failed to receive any message
                continue;
              }
              var id = MemoryMarshal.Read<ulong>(msg.AsSpan());
              var rt = MemoryMarshal.Read<MessageType>(msg.AsSpan().Slice(8, Unsafe.SizeOf<MessageType>()));
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
              else
              {
                if (!c.TryAdd(msg))
                {
                  // TODO: what kind of concurrent collection rejects an add? some sort of set?
                  continue;
                }
                if ((rt & MessageType.Final) != 0)
                  c.CompleteAdding();
              }
            } while (!cancellationToken.IsCancellationRequested);
          }
        }, cancellationToken);

      public IAsyncEnumerable<INngMsg> GetReplies(ulong msgId)
      {
        var messages = _outstanding.GetOrAdd(msgId, _ => new(new ConcurrentQueue<INngMsg>()));

        return messages.GetConsumer();
      }

      public void ClearReplies(ulong msgId)
      {
        if (_outstanding.TryRemove(msgId, out var q))
          q.Clear();
      }

      private async Task<TReply> UnaryRequest<TReply, TRequest>(TRequest item, CancellationToken cancellationToken)
        where TRequest : struct, IBigBufferTable
        where TReply : struct, IBigBufferTable
      {

        static bool HandleReply(INngMsg replyMsg, out TReply result)
        {
          var reply = replyMsg.ParseReply();

          var msgType = reply.Type();

          if ((msgType & MessageType.Control) != 0)
          {
            if ((msgType & MessageType.Final) != 0)
              throw new("The service was unable to handle the request.")
                { Data = { [typeof(INngMsg)] = replyMsg } };

            // TODO: handle other control messages
            Unsafe.SkipInit(out result);
            return false;
          }

          if ((msgType & MessageType.Final) == 0)
            throw new("The service had more than one result.")
              { Data = { [typeof(INngMsg)] = replyMsg } };

          var body = reply.Body();
          if (body.Length > 0)
          {
            ByteBuffer bb = new(body);
            result = new() { Model = new(bb, bb.Position) };
            return true;
          }

          Unsafe.SkipInit(out result);
          return false;
        }

        ulong msgId;
        using (var ctx = _pair.CreateAsyncContext(_factory).Unwrap())
          (await ctx.Send(_factory.CreateRequest(Utf8CallSend, item, out msgId))).Unwrap();

        var replies = GetReplies(msgId);

        try
        {
          await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
          {
            if (HandleReply(replyMsg, out var result))
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
      public async Task<Status> Send(Message message, CancellationToken cancellationToken)
        => await UnaryRequest<Status, Message>(message, cancellationToken);

      public async Task<Message> Receive(Empty empty, CancellationToken cancellationToken)
        => await UnaryRequest<Message, Empty>(empty, cancellationToken);
    }


    public abstract class Server : IRpcService
    {
      private readonly IPairSocket _pair;
      private readonly IAPIFactory<INngMsg> _factory;

      protected Server(IPairSocket pair, IAPIFactory<INngMsg> factory)
      {
        _pair = pair;
        _factory = factory;
      }

      private static Method ParseRequestInAsync(INngMsg nngMsg, out ByteBuffer bb)
      {
        var req = nngMsg.ParseRequest();
        var method = SelectMethod(req);
        bb = new(req.Body());
        return method;
      }

      private static Method SelectMethod(in RequestMessage req)
      {
        var serviceName = req.ServiceId();

        if (!serviceName.SequenceEqual(Utf8ServiceId))
          return default;

        var procName = req.ProcedureName();
        return
          procName.SequenceEqual(Utf8CallSend) ? Method.Send :
          procName.SequenceEqual(Utf8CallReceive) ? Method.Receive :
          default;
      }

      public Task RunAsync(CancellationToken cancellationToken)
      {
        // used as a concurrent set
        ConcurrentDictionary<Task, _> dispatched = new();

        async Task DispatcherLoop()
        {
          while (!cancellationToken.IsCancellationRequested)
          {
            INngMsg request;
            using (var ctx = _pair.CreateAsyncContext(_factory).Unwrap())
              request = (await ctx.Receive(cancellationToken)).Unwrap();
            var runner = Task.Run(async () => await Dispatch(request, cancellationToken), cancellationToken);
            var added = dispatched.TryAdd(runner, default);
            Debug.Assert(added);

            // sweep outstanding tasks to prevent leakage over long runtime
            foreach (var (task, _) in dispatched)
            {
              if (task.IsCompleted)
                dispatched.TryRemove(task, out var _);
            }
          }

          // wait on any remaining outstanding tasks
          foreach (var (task, _) in dispatched)
            if (!task.IsCompleted)
              await task;
        }

        return Task.Run(DispatcherLoop, cancellationToken);
      }
      private async Task Dispatch(INngMsg nngMsg, CancellationToken ct)
      {
        var method = ParseRequestInAsync(nngMsg, out var bb);

        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        var response = method switch
        {
          Method.Send => await CreateReplyAsync(async () => await Send(Message.GetRootAsMessage(bb), ct), nngMsg, ct),
          Method.Receive => await CreateReplyAsync(async () => await Receive(Empty.GetRootAsEmpty(bb), ct), nngMsg, ct),
          _ => OnUnhandledMessage(nngMsg, ct)
        };

        if (response is null)
        {
          response = _factory.CreateMessage();
          response.Append(nngMsg.AsSpan().Slice(0, 8));
          response.Append(MemoryMarshal.AsBytes(stackalloc[] { MessageType.FinalControl }));
          // body
          long errCode = 404;
          response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
          response.Append(Encoding.UTF8.GetBytes("Method Not Found"));

          using var ctx = _pair.CreateAsyncContext(_factory).Unwrap();
          await ctx.Send(response);
        }
        else if (response is not DoNotSendNngMsg)
        {
          using var ctx = _pair.CreateAsyncContext(_factory).Unwrap();
          await ctx.Send(response);
        }
      }
      private async ValueTask<INngMsg> CreateReplyAsync<T>(Func<ValueTask<T>> fn, INngMsg sourceMsg, CancellationToken ct)
        where T : struct, IBigBufferEntity
      {
        T result;
        try
        {
          result = await fn();
        }
        catch (UnauthorizedAccessException ex)
        {
          return UnauthorizedReply(sourceMsg, ex);
        }
        catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
        {
          return TimedOutReply(sourceMsg, ex);
        }
        catch (NotImplementedException ex)
        {
          return NotImplementedExceptionReply(sourceMsg, ex);
        }
        catch (HttpRequestException ex)
        {
          return UnhandledHttpExceptionReply(sourceMsg, ex);
        }
        catch (Exception ex)
        {
          return UnhandledExceptionReply(sourceMsg, ex);
        }

        return _factory.CreateReply(sourceMsg, result);
      }
      private unsafe INngMsg UnauthorizedReply(INngMsg nngMsg, Exception ex)
      {
        var response = _factory.CreateControlReply(nngMsg);
        long errCode = 401;
        response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
#if DEBUG
        response.Append(Encoding.UTF8.GetBytes(ex.Message));
#else
          response.Append(Encoding.UTF8.GetBytes("Unauthorized"));
#endif
        byte nullByte = 0;
        response.Append(MemoryMarshal.CreateReadOnlySpan(ref nullByte, 1));
        return response;
      }
      private unsafe INngMsg TimedOutReply(INngMsg nngMsg, Exception ex)
      {
        var response = _factory.CreateControlReply(nngMsg);
        long errCode = 408;
        response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
#if DEBUG
        response.Append(Encoding.UTF8.GetBytes(ex.Message));
#else
          response.Append(Encoding.UTF8.GetBytes("Request Timeout"));
#endif
        byte nullByte = 0;
        response.Append(MemoryMarshal.CreateReadOnlySpan(ref nullByte, 1));
        return response;
      }
      private unsafe INngMsg NotImplementedExceptionReply(INngMsg nngMsg, Exception ex)
      {
        var response = _factory.CreateControlReply(nngMsg);
        long errCode = 501;
        response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
#if DEBUG
        response.Append(Encoding.UTF8.GetBytes(ex.Message));
#else
          response.Append(Encoding.UTF8.GetBytes("Internal Server Error"));
#endif
        byte nullByte = 0;
        response.Append(MemoryMarshal.CreateReadOnlySpan(ref nullByte, 1));
        return response;
      }
      private INngMsg UnhandledHttpExceptionReply(INngMsg nngMsg, HttpRequestException ex)
      {
        var response = _factory.CreateControlReply(nngMsg);
        var errCode = (long)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
        response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
        response.Append(Encoding.UTF8.GetBytes(ex.Message));
        byte nullByte = 0;
        response.Append(MemoryMarshal.CreateReadOnlySpan(ref nullByte, 1));
        return response;
      }
      private unsafe INngMsg UnhandledExceptionReply(INngMsg nngMsg, Exception ex)
      {
        var response = _factory.CreateControlReply(nngMsg);
        long errCode = 500;
        response.Append(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref errCode, 1)));
#if DEBUG
        response.Append(Encoding.UTF8.GetBytes(ex.GetType().FullName!));
        ReadOnlySpan<byte> sepBytes = stackalloc[] { (byte)':', (byte)' ' };
        response.Append(sepBytes);
        response.Append(Encoding.UTF8.GetBytes(ex.Message));
#else
          response.Append(Encoding.UTF8.GetBytes("Not Implemented"));
#endif
        byte nullByte = 0;
        response.Append(MemoryMarshal.CreateReadOnlySpan(ref nullByte, 1));
        return response;
      }


      // implementation follows
      protected virtual INngMsg? OnUnhandledMessage(INngMsg msg, CancellationToken cancellationToken)
        => OnUnhandledMessage(cancellationToken);

      protected virtual INngMsg? OnUnhandledMessage(CancellationToken cancellationToken)
        => null;

      public abstract Task<Status> Send(Message message, CancellationToken cancellationToken);

      public abstract Task<Message> Receive(Empty empty, CancellationToken cancellationToken);
    }
  }
}
