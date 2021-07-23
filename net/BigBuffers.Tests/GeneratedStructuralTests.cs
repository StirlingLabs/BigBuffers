using System;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
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
      sp1.Fill("Hello World");

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
      x.Fill(new[] { "Hello", "World" });

      var t = TableF.GetRootAsTableF(bb.ByteBuffer);

      Assert.AreEqual("Hello", t.X(0));
      Assert.AreEqual("World", t.X(1));
    }

    [Test]
    public void TableGTest()
    {
      var bb = new BigBufferBuilder();
      TableG.StartTableG(bb);
      TableG.AddX(bb, bb.CreateVector(out var x));
      TableG.EndTableG(bb);

      var bkp = bb.Offset;
      Assert.Throws<InvalidOperationException>(() => {
        x.Fill(new[]
        {
          StructG.CreateStructG(bb, true),
          StructG.CreateStructG(bb, false),
          StructG.CreateStructG(bb, true),
          StructG.CreateStructG(bb, false)
        });
      });
      bb.Offset = bkp;

      x.FillInline(() => new[]
      {
        StructG.CreateStructG(bb, true),
        StructG.CreateStructG(bb, false),
        StructG.CreateStructG(bb, true),
        StructG.CreateStructG(bb, false)
      });

      var t = TableG.GetRootAsTableG(bb.ByteBuffer);

      Assert.NotNull(t.X(0));
      Assert.NotNull(t.X(1));
      Assert.NotNull(t.X(2));
      Assert.NotNull(t.X(3));

      Assert.IsTrue(t.X(0)?.X);
      Assert.IsFalse(t.X(1)?.X);
      Assert.IsTrue(t.X(2)?.X);
      Assert.IsFalse(t.X(3)?.X);
    }

    [Test]
    public void TableHTest()
    {
      var bb = new BigBufferBuilder();
      TableH.StartTableH(bb);
      TableH.AddX(bb, TableH.CreateXVector(bb, out var x));
      TableH.EndTableH(bb);
      x.Fill(new[] { true, false, true, false });

      var t = TableH.GetRootAsTableH(bb.ByteBuffer);

      Assert.NotNull(t.X(0));
      Assert.NotNull(t.X(1));
      Assert.NotNull(t.X(2));
      Assert.NotNull(t.X(3));

      Assert.IsTrue(t.X(0));
      Assert.IsFalse(t.X(1));
      Assert.IsTrue(t.X(2));
      Assert.IsFalse(t.X(3));

    }

    [Test]
    public void TableITest()
    {
      var bb = new BigBufferBuilder();
      TableI.StartTableI(bb);
      TableI.AddX(bb, bb.CreateVector(out var x));
      TableI.EndTableI(bb);

      var bkp = bb.Offset;
      Assert.Throws<InvalidOperationException>(() => {
        x.Fill(new[]
        {
          StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { true, false }),
          StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { true, true }),
          StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { false, false }),
          StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { false, true }),
        });
      });
      bb.Offset = bkp;

      x.FillInline(() => new[]
      {
        StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { true, false }),
        StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { true, true }),
        StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { false, false }),
        StructI.CreateStructI(bb, (ReadOnlyBigSpan<bool>)new[] { false, true }),
      });

      var t = TableI.GetRootAsTableI(bb.ByteBuffer);

      Assert.NotNull(t.X(0));
      Assert.NotNull(t.X(1));
      Assert.NotNull(t.X(2));
      Assert.NotNull(t.X(3));

      Assert.IsTrue(t.X(0)!.Value.X(0));
      Assert.IsFalse(t.X(0)!.Value.X(1));

      Assert.IsTrue(t.X(1)!.Value.X(0));
      Assert.IsTrue(t.X(1)!.Value.X(1));

      Assert.IsFalse(t.X(2)!.Value.X(0));
      Assert.IsFalse(t.X(2)!.Value.X(1));

      Assert.IsFalse(t.X(3)!.Value.X(0));
      Assert.IsTrue(t.X(3)!.Value.X(1));
    }

    [Test]
    public void TableJTest()
    {
      var bb = new BigBufferBuilder();

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var x));
      var to = TableJ.EndTableJ(bb);
      x.Fill(new[] { to });

      var t = TableJ.GetRootAsTableJ(bb.ByteBuffer);
      
      var t2 = t.X(0);
      Assert.NotNull(t2);
      var t3 = t2!.Value.X(0);
      Assert.NotNull(t3);
    }
    
    [Test]
    public void TableKTest()
    {
      var bb = new BigBufferBuilder();

      TableK.StartTableK(bb);
      TableK.AddX(bb, bb.CreateVector(out var kx));
      TableK.EndTableK(bb);
      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var to = TableJ.EndTableJ(bb);
      jx.Fill(new[] { to });
      kx.Fill(new[] { to });

      var t = TableK.GetRootAsTableK(bb.ByteBuffer);
      
      var t2 = t.X(0);
      Assert.NotNull(t2);
      var t3 = t2!.Value.X(0);
      Assert.NotNull(t3);
      var t4 = t3!.Value.X(0);
      Assert.NotNull(t4);
    }
    
    [Test]
    public void TableLTest()
    {
      var bb = new BigBufferBuilder();

      TableL.StartTableL(bb);
      TableL.AddX(bb, bb.CreateOffset<TableK>(out var lx));
      TableL.EndTableL(bb);
      TableK.StartTableK(bb);
      TableK.AddX(bb, bb.CreateVector(out var kx));
      var ko = TableK.EndTableK(bb);
      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });
      kx.Fill(new[] { jo });
      lx.Fill(ko);

      var t = TableL.GetRootAsTableL(bb.ByteBuffer);
      
      var t2 = t.X;
      Assert.NotNull(t2);
      var t3 = t2!.Value.X(0);
      Assert.NotNull(t3);
      var t4 = t3!.Value.X(0);
      Assert.NotNull(t4);
    }
  }
}
