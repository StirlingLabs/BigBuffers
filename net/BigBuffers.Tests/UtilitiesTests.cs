using System.Runtime.CompilerServices;
using FluentAssertions;
using NUnit.Framework;

namespace BigBuffers.Tests
{
  public static class UtilitiesTests
  {
    [Test]
    public static void TypeEqualsTest()
    {
      var o1 = new object();
      var o2 = new object();
      var o3 = new object();
      // these compare the same type
      o1.TypeEquals(o2).Should().BeTrue();
      o1.TypeEquals(o3).Should().BeTrue();
      o2.TypeEquals(o3).Should().BeTrue();

      {
        var a = new StrongBox<short>(1);
        var b = new StrongBox<int>(1);
        var c = new StrongBox<int>(1);
        var d = new StrongBox<ulong>(1);

        // these compare generics of the same type
        a.TypeEquals(b).Should().BeFalse();
        a.TypeEquals(b).Should().BeFalse();
        b.TypeEquals(c).Should().BeTrue();
        a.TypeEquals(d).Should().BeFalse();
        b.TypeEquals(d).Should().BeFalse();

        // these compare different types
        a.TypeEquals(o1).Should().BeFalse();
        b.TypeEquals(o1).Should().BeFalse();
        c.TypeEquals(o1).Should().BeFalse();
        d.TypeEquals(o1).Should().BeFalse();
      }

    }
  }
}
