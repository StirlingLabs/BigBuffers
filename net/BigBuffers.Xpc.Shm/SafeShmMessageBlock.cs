using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.SafeShm;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Extensions;

namespace BigBuffers.Xpc.Shm; 

[PublicAPI]
public sealed class SafeShmMessageBlock : IDisposable {

  private bool _disposed;

  private readonly unsafe void* _pMem;

  private readonly nuint _length;

  private readonly Mapping _mapping;

  private readonly nint _remotePid;

  public bool IsConnected { get; private set; }

  public event Action<SafeShmMessageBlock> Connected;

  private unsafe SafeShmMessageBlock(void* pMemory, nuint length, Mapping mapping, nint remotePid) {
    _pMem = pMemory;
    _length = length;
    _mapping = mapping;
    _remotePid = remotePid;
  }

  private unsafe SafeShmMessageBlock(void* pMemory, nuint length, nint remotePid) {
    _pMem = pMemory;
    _length = length;
    _remotePid = remotePid;
  }

  internal static unsafe SafeShmMessageBlock CreateOffer(uint connectionKey, nint remotePid, CancellationToken ct = default) {
    var mapping = Mapping.Create(SafeShmShelfHeader.Pages, out void* pMemory);
    var block = new SafeShmMessageBlock(pMemory, SafeShmShelfHeader.AllocationSize, mapping, remotePid);
    block.Header.Initialize(connectionKey);
    var offer = mapping.Offer(remotePid);

    void OfferWatcher() {
      for (;;) {
        if (ct.IsCancellationRequested) {
          offer.Stop();
          break;
        }

        if (offer.CheckState() == OfferState.Pending) {
          Thread.Yield();
          continue;
        }

        block.OnConnected();
        break;
      }
    }

    ThreadPoolHelpers.QueueUserWorkItemFast(OfferWatcher);
    return block;
  }

  private void OnConnected() {
    IsConnected = true;
    Connected?.Invoke(this);
  }

  [CanBeNull]
  private static unsafe SafeShmMessageBlock JoinInternal(nint remotePid) {
    var success = API.Join(remotePid, out var pMemory, out var length);
    if (!success) return null;

    return new(pMemory, length, remotePid);
  }

  internal static Task<SafeShmMessageBlock> Join(nint remotePid, Func<SafeShmMessageBlock, bool> inspector) {
    var blocks = _joinedBlocksByProcess.GetOrAdd(remotePid, _ => new());
    foreach (var (block, _) in blocks) {
      if (inspector(block) && blocks.TryRemove(block, out var _))
        return Task.FromResult(block);
    }

    var tcs = new TaskCompletionSource<SafeShmMessageBlock>();
    var completion = new SafeShmJoinRequest(inspector, tcs);

    AddJoinRequest(remotePid, completion);

    return tcs.Task;
  }

  private static void AddJoinRequest(nint remotePid, SafeShmJoinRequest request) {
    _joinRequests.AddOrUpdate(remotePid,
      new ConcurrentDictionary<SafeShmJoinRequest, _> { [request] = default },
      (_, d) => {
        d.TryAdd(request, default);
        return d;
      });
    OnJoinRequestAdded();
  }

  private static void OnJoinRequestAdded() {
    lock (_joinRequestThreadCancellation) {
      if (_joinRequestThread is not null && _joinRequestThread.IsAlive)
        return;

      _joinRequestThread = new(JoinRequestThreadLoop);
      _joinRequestThread.Start();
    }
  }

  private static CancellationTokenSource _joinRequestThreadCancellation = new();

  private static ConcurrentDictionary<nint, ConcurrentDictionary<SafeShmJoinRequest, _>> _joinRequests = new();

  private static ConcurrentDictionary<nint, ConcurrentDictionary<SafeShmMessageBlock, _>> _joinedBlocksByProcess = new();

  private static Thread _joinRequestThread;

  private static void JoinRequestThreadLoop() {
    while (JoinRequestThreadLoopBody())
      Thread.Yield();
  }

  private static bool JoinRequestThreadLoopBody() {
    if (_joinRequests.IsEmpty || _joinRequestThreadCancellation.IsCancellationRequested) return false;

    foreach (var (remotePid, completions) in _joinRequests) {
      var joined = JoinInternal(remotePid);
      if (joined is null) continue;

      foreach (var (completion, _) in completions) {
        // ReSharper disable once InvertIf
        if (!completion.Completion.Task.IsCompleted
            && completion.Inspector(joined)
            && completions.TryRemove(completion, out var _)) {
          completion.Completion.TrySetResult(joined);
          break;
        }
      }
    }

    return true;
  }

  internal unsafe ref SafeShmShelfHeader Header
    => ref Unsafe.AsRef<SafeShmShelfHeader>(_pMem);

  private unsafe long* Messages
    => (long*)((nint)_pMem + sizeof(SafeShmShelfHeader));

  public ushort Count => Header.Count;

  public ushort Capacity => SafeShmShelfHeader.Capacity;

  public ref long GetMessageSlot(nint index) {
    if (index >= Capacity)
      throw new ArgumentOutOfRangeException(nameof(index));
    if (_disposed)
      throw new ObjectDisposedException(nameof(SafeShmMessageBlock));

    return ref GetMessageSlotInternal(index);
  }

  private unsafe ref long GetMessageSlotInternal(nint index)
    => ref Messages![index];

  public bool LinearScanForMessage(ref nint index, out long msg) {
    if (_disposed)
      throw new ObjectDisposedException(nameof(SafeShmMessageBlock));

    for (; index < Capacity; ++index) {
      msg = GetMessageSlotInternal(index);
      if (msg != default)
        return true;
    }

    msg = default;
    return false;
  }

  public void Dispose()
    => _disposed = true;

}
