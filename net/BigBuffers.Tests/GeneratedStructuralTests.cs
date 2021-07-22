using System.Diagnostics;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using Generated;
using NUnit.Framework;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Assertions;

namespace BigBuffers.Tests
{
  public class GeneratedStructuralTests
  {
    [Test]
    public void HashStructTest()
    {
      var bb = new BigBufferBuilder();
      var bytes = new byte[]
      {
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
      };
      var h = Hash.CreateHash(bb, new(bytes));

    }
    [Test]
    public void TestTableTest()
    {
      var bb = new BigBufferBuilder();
      Generated.Test.StartTest(bb);
      var initHashBytes = new byte[]
      {
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
      };
      var oh = Hash.CreateHash(bb, new(initHashBytes));
      Generated.Test.AddHash(bb, oh);
      var ot = Generated.Test.EndTest(bb);
      Assert.AreEqual(8 + 32 + 3 * 2, bb.Offset);

      var t = Generated.Test.GetRootAsTest(bb.ByteBuffer);

      Assert.AreEqual(0, t._model.Offset);

      Assert.NotNull(t.Hash);

      var h = t.Hash.Value;

      for (var i = 0u; i < 32; ++i)
        Assert.AreEqual(initHashBytes[i], h.Bytes(i));

      var s = h.Bytes();

      for (var i = 0u; i < 32; ++i)
        Assert.AreEqual(initHashBytes[i], s[i]);

      s.AsSmallSlices(RandomNumberGenerator.Fill);

      s[1u] = 0;

      s.AsPinnedEnumerable(ps =>
        CollectionAssert.AreNotEqual(initHashBytes, ps));

      for (var i = 0u; i < 32; ++i)
        Assert.AreEqual(s[i], h.Bytes(i));
    }

    [Test]
    public void TableATest()
    {
      var bb = new BigBufferBuilder();
      TableA.StartTableA(bb);
      TableA.AddX(bb, 0x0102030405060708uL);
      TableA.EndTableA(bb);

      var t = TableA.GetRootAsTableA(bb.ByteBuffer);

      Assert.AreEqual(0x0102030405060708uL, t.X);
    }

    [Test]
    public void TableBTest()
    {
      var bb = new BigBufferBuilder();
      TableB.StartTableB(bb);
      TableB.AddX(bb, StructA.CreateStructA(bb, 0x0102030405060708uL));
      TableB.EndTableB(bb);

      var t = TableB.GetRootAsTableB(bb.ByteBuffer);

      Assert.NotNull(t.X);

      Assert.AreEqual(0x0102030405060708uL, t.X.Value.GetX());

      t.X.Value.X = 0x0807060504030201uL;

      Assert.AreEqual(0x0807060504030201uL, t.X.Value.X);

      t.X.Value.SetX(0x0102030405060708uL);

      Assert.AreEqual(0x0102030405060708uL, t.X.Value.X);
    }

    [Test]
    public void TableCTest()
    {
      var a = new byte[]
      {
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,
        0, 1, 2, 3, 4, 5, 6, 7,

      };
      var b = new byte[]
      {
        7, 6, 5, 4, 3, 2, 1, 0,
        7, 6, 5, 4, 3, 2, 1, 0,
        7, 6, 5, 4, 3, 2, 1, 0,
        7, 6, 5, 4, 3, 2, 1, 0,
      };

      var bb = new BigBufferBuilder();
      TableC.StartTableC(bb);
      TableC.AddX(bb, StructB.CreateStructB(bb, new(a)));
      TableC.EndTableC(bb);

      var t = TableC.GetRootAsTableC(bb.ByteBuffer);

      Assert.NotNull(t.X);

      BigSpanAssert.AreEqual(new(a), t.X.Value.X());

      b.CopyTo(t.X.Value.X());

      BigSpanAssert.AreEqual(new(b), t.X.Value.X());

    }

    [Test]
    public void TableDTest()
    {
      var bb = new BigBufferBuilder();
      TableD.StartTableD(bb);
      TableD.AddX(bb, 0x0102030405060708uL);
      TableD.EndTableD(bb);

      var t = TableD.GetRootAsTableD(bb.ByteBuffer);

      Assert.AreEqual(0x0102030405060708uL, t.X);
    }

    [Test]
    public void TableETest()
    {
      var bb = new BigBufferBuilder();
      TableE.StartTableE(bb);
      TableE.AddX(bb, bb.CreateString(out var sp1));
      TableE.EndTableE(bb);
      sp1.Set("Hello World");

      var t = TableE.GetRootAsTableE(bb.ByteBuffer);

      Assert.AreEqual("Hello World", t.X);

      var utf8 = new BigSpan<byte>(Encoding.UTF8.GetBytes("Hello World"));
      var xSpan = t.GetXSpan();
      for (var i = 0u; i < 11; ++i)
        Assert.AreEqual(utf8[i], xSpan[i]);
      Assert.AreEqual((byte)0, xSpan[11u]);
    }

    [Test]
    public void TableFTest()
    {
      var bb = new BigBufferBuilder();
      TableF.StartTableF(bb);
      TableF.AddX(bb, TableF.CreateXVector(bb, out var x));
      TableF.EndTableF(bb);
      x.Set(new[] { "Hello", "World" });

      var t = TableF.GetRootAsTableF(bb.ByteBuffer);

      Assert.AreEqual("Hello", t.X(0));
      Assert.AreEqual("World", t.X(1));
    }
  }
}
