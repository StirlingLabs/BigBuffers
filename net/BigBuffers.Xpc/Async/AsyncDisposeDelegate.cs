using System;
using System.Threading.Tasks;

namespace BigBuffers.Xpc.Async
{
  public sealed class AsyncDisposeDelegate : IAsyncDisposable
  {
    private readonly Func<ValueTask> _dlg;
    public AsyncDisposeDelegate(Func<ValueTask> dlg)
      => _dlg = dlg;

    public async ValueTask DisposeAsync()
      => await _dlg();
  }
}
