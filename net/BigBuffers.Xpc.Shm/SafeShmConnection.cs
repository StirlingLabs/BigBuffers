using System;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Extensions;

namespace BigBuffers.Xpc.Shm; 

[PublicAPI]
public readonly struct SafeShmConnection : ICloneable {

  public readonly SafeShmMessageBlock Outbound, Inbound;

  public SafeShmConnection(SafeShmMessageBlock outbound, SafeShmMessageBlock inbound) {
    Outbound = outbound;
    Inbound = inbound;
  }

  public SafeShmConnection(in SafeShmConnection source) {
    Outbound = source.Outbound;
    Inbound = source.Inbound;
  }


  private static uint _connectionKey = GenerateConnectionKey();

  private static uint GenerateConnectionKey() {
    Span<uint> connectionKeySpan = stackalloc uint[] { 0 };
    ref var connectionKey = ref connectionKeySpan[0];
    do {
      Security.FillWithRandomData(MemoryMarshal.AsBytes(connectionKeySpan));
      connectionKey &= int.MaxValue;
    } while (connectionKey == 0);

    return connectionKey;
  }

  public static Task<SafeShmConnection> ConnectAsync(nint remotePid, CancellationToken ct = default) {
    var connectionKey = _connectionKey.AtomicIncrement();
    var offered = SafeShmMessageBlock.CreateOffer(connectionKey, remotePid, ct);
    return JoinInternalAsync(remotePid, ct, connectionKey, offered);
  }

  private static async Task<SafeShmConnection> JoinInternalAsync(nint remotePid, CancellationToken ct, uint connectionKey, SafeShmMessageBlock offered) {
    SafeShmMessageBlock joined = null;
    while (!ct.IsCancellationRequested) {
      var connectionKeyCopy = connectionKey;
      joined = await SafeShmMessageBlock.Join(remotePid, block => {
        ref var header = ref block.Header;
        return header.Magic == SafeShmShelfHeader.MagicValue
          && header.ConnectionKey == connectionKeyCopy;
      });
      if (joined is not null) break;

      await Task.Yield();
    }

    return new(offered, joined);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public void Deconstruct(out SafeShmMessageBlock outbound, out SafeShmMessageBlock inbound) {
    outbound = Outbound;
    inbound = Inbound;
  }

  object ICloneable.Clone() => this;

  public SafeShmConnection Clone() => this;

}
