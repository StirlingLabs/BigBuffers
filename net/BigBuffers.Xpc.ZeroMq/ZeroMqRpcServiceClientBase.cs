using System;
using System.IO;
using System.Net.Http;
using JetBrains.Annotations;
using ZeroMQ;

namespace BigBuffers.Xpc.ZeroMq
{
  [PublicAPI]
  public class ZeroMqRpcServiceClientBase : IDisposable
  {
    protected readonly TextWriter? _logger;

    public ZeroMqRpcServiceClientBase(ZSocket socket, TextWriter? logger = null)
    {
      Socket = socket ?? throw new ArgumentNullException(nameof(socket));
      _logger = logger;
    }

    public ZSocket Socket { get; protected set; }

    public void Dispose()
      => Socket.Dispose();
  }
}
