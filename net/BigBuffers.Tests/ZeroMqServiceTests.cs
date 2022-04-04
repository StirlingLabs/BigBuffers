using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using nng;
using NUnit.Framework;
using ZeroMQ;

#nullable enable

namespace BigBuffers.Tests
{
  [Timeout(300000)] // 5 minutes
  [NonParallelizable]
  [FixtureLifeCycle(LifeCycle.SingleInstance)]
  public class ZeroMqRpcServiceTests
  {
    private static int _lastIssuedFreeEphemeralTcpPort = -1;
    private static int GetFreeEphemeralTcpPort()
    {
      bool IsFree(int realPort)
      {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        IPEndPoint[] listeners = properties.GetActiveTcpListeners();
        int[] openPorts = listeners.Select(item => item.Port).ToArray<int>();
        return openPorts.All(openPort => openPort != realPort);
      }

      const int ephemeralRangeSize = 16384;
      const int ephemeralRangeStart = 49152;

      var port = (_lastIssuedFreeEphemeralTcpPort + 1) % ephemeralRangeSize;

      while (!IsFree(ephemeralRangeStart + port))
        port = (port + 1) % ephemeralRangeSize;

      _lastIssuedFreeEphemeralTcpPort = port;

      return ephemeralRangeStart + port;
    }
    public static IEnumerable<string> GetLocalTestUrls()
    {
      yield return "inproc://ZeroMqLocalTest";
      yield return "tcp://127.0.0.1:" + GetFreeEphemeralTcpPort();
      yield return "tcp://[::1]:" + GetFreeEphemeralTcpPort();
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        yield return $"ipc://ZeroMqLocalTest-{Environment.ProcessId}";
      else
      {
        var dir = $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/.zmq";
        var path = $"{dir}/{Environment.ProcessId}";
        Directory.CreateDirectory(dir);
        yield return $"ipc://{path}";
      }
      //yield return "udp://127.0.0.1:" + GetFreeEphemeralTcpPort();
      //yield return "udp://[::1]:" + GetFreeEphemeralTcpPort();
    }

  }
}
