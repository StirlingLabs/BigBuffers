using System;
using System.IO;
using System.Net.Http;

namespace BigBuffers.Xpc.Http
{
  public class HttpRpcServiceClientBase : IDisposable
  {
    protected readonly TextWriter? _logger;

    public HttpRpcServiceClientBase(HttpClient httpClient, TextWriter? logger = null)
    {
      HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
      _logger = logger;
    }

    public HttpClient HttpClient { get; protected set; }

    public void Dispose()
      => HttpClient.Dispose();
  }
}
