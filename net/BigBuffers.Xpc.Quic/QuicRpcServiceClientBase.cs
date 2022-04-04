#nullable enable
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Quic;
using StirlingLabs.MsQuic;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Collections;

namespace BigBuffers.Xpc.Quic;

[PublicAPI]
public abstract class QuicRpcServiceClientBase : QuicRpcServiceContext
{
  protected abstract SizedUtf8String Utf8ServiceId { get; }

  private readonly ConcurrentDictionary<long, AsyncProducerConsumerCollection<ReplyMessage>> _outstandingReplies = new();

  protected QuicRpcServiceClientBase(QuicPeerConnection connection, TextWriter logger)
    : base(connection, logger, false) { }

  protected IAsyncEnumerable<IMessage> GetReplies(long msgId)
  {
    var messages = _outstandingReplies.GetOrAdd(msgId, _ => new(new ConcurrentQueue<ReplyMessage>()));

    return messages.GetConsumer();
  }

  protected void ClearReplies(long msgId)
  {
    if (_outstandingReplies.TryRemove(msgId, out var q))
      q.Clear();
  }

  protected abstract SizedUtf8String ResolveMethodSignature<TMethodEnum>(TMethodEnum method) where TMethodEnum : struct, Enum;

  protected async Task<TReply> UnaryRequest<TMethodEnum, TReply, TRequest>(TMethodEnum method, TRequest item,
    CancellationToken cancellationToken)
    where TMethodEnum : struct, Enum
    where TRequest : struct, IBigBufferTable
    where TReply : struct, IBigBufferTable
  {
    var msgId = SharedCounters.ReadAndIncrementMessageCount();

    var bigMemory = new BigMemory<byte>(item.Model.ByteBuffer.ToSizedMemory());

    var req = new RequestMessage(this, MessageType.Normal, bigMemory, msgId)
    {
      ServiceId = Utf8ServiceId,
      RpcMethod = ResolveMethodSignature(method)
    };

    await req.SendAsync();

    var replies = GetReplies(req.Id);

    try
    {
      await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
      {
        if (HandleMessage(replyMsg, out TReply result, true))
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

  protected async Task<TReply> ClientStreamingRequest<TMethodEnum, TReply, TRequest>(TMethodEnum method, ChannelReader<TRequest> itemsReader,
    CancellationToken cancellationToken)
    where TMethodEnum : struct, Enum
    where TRequest : struct, IBigBufferTable
    where TReply : struct, IBigBufferTable
  {
    var msgId = SharedCounters.ReadAndIncrementMessageCount();

    var items = itemsReader.AsConsumingAsyncEnumerable(cancellationToken);

    var sending = Task.Run(async () => {

      await foreach (var item in items.WithCancellation(cancellationToken))
      {
        var bigMemory = new BigMemory<byte>(item.Model.ByteBuffer.ToSizedMemory());

        var req = new RequestMessage(this, MessageType.Normal, bigMemory, msgId)
        {
          ServiceId = Utf8ServiceId,
          RpcMethod = ResolveMethodSignature(method)
        };

        await req.SendAsync();
      }

      var final = new RequestMessage(this, MessageType.FinalControl, new(), msgId)
      {
        ServiceId = Utf8ServiceId,
        RpcMethod = ResolveMethodSignature(method)
      };

      await final.SendAsync();

    }, cancellationToken);

    var replies = GetReplies(msgId);

    try
    {
      await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
      {
        if (HandleMessage(replyMsg, out TReply result, true))
          return result;
      }
    }
    finally
    {
      await sending;

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
    var msgId = SharedCounters.ReadAndIncrementMessageCount();

    var bigMemory = new BigMemory<byte>(item.Model.ByteBuffer.ToSizedMemory());

    var req = new RequestMessage(this, MessageType.Normal, bigMemory, msgId)
    {
      ServiceId = Utf8ServiceId,
      RpcMethod = ResolveMethodSignature(method)
    };

    await req.SendAsync();

    var replies = GetReplies(req.Id);

    try
    {
      await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
      {
        if (HandleMessage(replyMsg, out TReply result, false))
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
    ChannelReader<TRequest> itemsReader,
    [EnumeratorCancellation] CancellationToken cancellationToken)
    where TMethodEnum : struct, Enum
    where TRequest : struct, IBigBufferTable
    where TReply : struct, IBigBufferTable
  {

    var msgId = SharedCounters.ReadAndIncrementMessageCount();

    var items = itemsReader.AsConsumingAsyncEnumerable(cancellationToken);

    var sending = Task.Run(async () => {

      await foreach (var item in items.WithCancellation(cancellationToken))
      {
        var bigMemory = new BigMemory<byte>(item.Model.ByteBuffer.ToSizedMemory());

        var req = new RequestMessage(this, MessageType.Normal, bigMemory, msgId)
        {
          ServiceId = Utf8ServiceId,
          RpcMethod = ResolveMethodSignature(method)
        };

        await req.SendAsync();
      }

      var final = new RequestMessage(this, MessageType.FinalControl, new(), msgId)
      {
        ServiceId = Utf8ServiceId,
        RpcMethod = ResolveMethodSignature(method)
      };

      await final.SendAsync();

    }, cancellationToken);

    var replies = GetReplies(msgId);

    try
    {
      await foreach (var replyMsg in replies.WithCancellation(cancellationToken))
      {
        if (HandleMessage(replyMsg, out TReply result, false))
          yield return result;
      }
    }
    finally
    {
      await sending;

      ClearReplies(msgId);
    }
  }
}
