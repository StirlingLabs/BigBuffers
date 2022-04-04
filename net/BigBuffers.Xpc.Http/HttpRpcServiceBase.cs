using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using BigBuffers.Xpc.Async;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BigBuffers.Xpc.Http
{
  public static class HttpRpcServiceBase
  {
    public static void UseHttpRpcService<T>(this IApplicationBuilder ab, T svc)
      where T : HttpRpcServiceServerBase
    {
      /*ab.Use(async (ctx, next) => {
        var body = ctx.Response.Body;
        if (HttpProtocol.IsHttp11(ctx.Request.Protocol))
        {
          var forceAsyncStream = new ForceAsyncStream(body);
          var chunkedStream = new Http11ChunkedBodyAwareStream(forceAsyncStream, ctx);
          ctx.Response.Body = chunkedStream;
        }
        await next();

        if (ctx.Response.Body != body)
          ctx.Response.Body = body;
      });*/
      ab.UseRouter(svc);
    }
    internal static readonly object SyncItemKey = new();


    internal static AsyncCountdownEvent GetCompletionHoldEvent(this HttpContext ctx)
    {
      AsyncCountdownEvent ace = null!;
      try
      {
        lock (SyncItemKey)
        {
          if (ctx.Items.TryGetValue(SyncItemKey, out var o))
            return ace = (AsyncCountdownEvent)o!;
          else
          {
            ctx.Items[SyncItemKey] = ace = new(1);
            return ace;
          }
        }
      }
      finally
      {
#if DEBUG
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        Debug.Assert(ace is not null);
        var svc = ctx.GetRouteData().Routers.OfType<HttpRpcServiceServerBase>().Single();
        var req = ctx.Request;
        var rsp = ctx.Response;
        svc.Logger?.WriteLine(
          $"[{TimeStamp:F3}] HttpRpcServiceBase T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{ace.CurrentCount}: GetCompletionHoldEvent");
#endif
      }

    }

    [Discardable]
    private static double TimeStamp => SharedCounters.GetTimeSinceStarted().TotalSeconds;

    public static void AcquireCompletionHold(this HttpContext ctx)
    {
      var e = ctx.GetCompletionHoldEvent();
      e.AddCount();

#if DEBUG
      var svc = ctx.GetRouteData().Routers.OfType<HttpRpcServiceServerBase>().Single();
      var req = ctx.Request;
      var rsp = ctx.Response;
      svc.Logger?.WriteLine(
        $"[{TimeStamp:F3}] HttpRpcServiceBase T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{e.CurrentCount}: AcquireCompletionHold");
#endif
    }

    public static void ReleaseCompletionHold(this HttpContext ctx)
    {
      var e = ctx.GetCompletionHoldEvent();
      e.Signal();

#if DEBUG
      var svc = ctx.GetRouteData().Routers.OfType<HttpRpcServiceServerBase>().Single();
      var req = ctx.Request;
      var rsp = ctx.Response;
      svc.Logger?.WriteLine(
        $"[{TimeStamp:F3}] HttpRpcServiceBase T{Task.CurrentId} X{ctx.TraceIdentifier} Q{req.GetHashCode():X}:{req.Path} R{rsp.GetHashCode():X} H{e.CurrentCount}: ReleaseCompletionHold");
#endif
    }

    public static long CountCompletionHolds(this HttpContext ctx)
    {
      lock (SyncItemKey)
      {
        return ctx.Items.TryGetValue(SyncItemKey, out var ace)
          ? ((AsyncCountdownEvent)ace!).CurrentCount
          : 0;
      }
    }
  }
}
