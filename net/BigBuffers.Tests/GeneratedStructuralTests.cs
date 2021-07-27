using System;
using System.Diagnostics;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using FluentAssertions.Common;
using Generated;
using NUnit.Framework;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Assertions;

namespace BigBuffers.Tests
{
  public static class GeneratedStructuralTests
  {
    [Test]
    public static void HashStructTest()
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
    public static void TestTableTest()
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
      ot.Value.Should().Be(0uL);

      (8 + 32 + 3 * 2).Should().IsSameOrEqualTo(bb.Offset);

      var t = Generated.Test.GetRootAsTest(bb.ByteBuffer);

      0.Should().IsSameOrEqualTo(t._model.Offset);

      t.Hash.Should().NotBeNull();

      var h = t.Hash!.Value;

      for (var i = 0u; i < 32; ++i)
        initHashBytes[i].Should().IsSameOrEqualTo(h.Bytes(i));

      var s = h.Bytes();

      for (var i = 0u; i < 32; ++i)
        initHashBytes[i].Should().IsSameOrEqualTo(s[i]);

      s.AsSmallSlices(RandomNumberGenerator.Fill);

      s[1u] = 0;

      s.AsPinnedEnumerable(ps =>
        CollectionAssert.AreNotEqual(initHashBytes, ps));

      for (var i = 0u; i < 32; ++i)
        s[i].Should().IsSameOrEqualTo(h.Bytes(i));
    }

    [Test]
    public static void TableATest()
    {
      var bb = new BigBufferBuilder();
      TableA.StartTableA(bb);
      TableA.AddX(bb, 0x0102030405060708uL);
      TableA.EndTableA(bb);

      var t = TableA.GetRootAsTableA(bb.ByteBuffer);

      0x0102030405060708uL.Should().IsSameOrEqualTo(t.X);
    }

    [Test]
    public static void TableBTest()
    {
      var bb = new BigBufferBuilder();
      TableB.StartTableB(bb);
      TableB.AddX(bb, StructA.CreateStructA(bb, 0x0102030405060708uL));
      TableB.EndTableB(bb);

      var t = TableB.GetRootAsTableB(bb.ByteBuffer);

      t.X.Should().NotBeNull();

      0x0102030405060708uL.Should().IsSameOrEqualTo(t.X!.Value.GetX());

      t.X.Value.X = 0x0807060504030201uL;

      0x0807060504030201uL.Should().IsSameOrEqualTo(t.X.Value.X);

      t.X.Value.SetX(0x0102030405060708uL);

      0x0102030405060708uL.Should().IsSameOrEqualTo(t.X.Value.X);
    }

    [Test]
    public static void TableCTest()
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

      t.X.Should().NotBeNull();

      BigSpanAssert.AreEqual(BigSpan.Create(a), t.X!.Value.X());

      b.CopyTo(t.X.Value.X());

      BigSpanAssert.AreEqual(BigSpan.Create(b), t.X!.Value.X());

    }

    [Test]
    public static void TableDTest()
    {
      var bb = new BigBufferBuilder();
      TableD.StartTableD(bb);
      TableD.AddX(bb, 0x0102030405060708uL);
      TableD.EndTableD(bb);

      var t = TableD.GetRootAsTableD(bb.ByteBuffer);

      0x0102030405060708uL.Should().IsSameOrEqualTo(t.X);
    }

    [Test]
    public static void TableETest()
    {
      var bb = new BigBufferBuilder();
      TableE.StartTableE(bb);
      TableE.AddX(bb, bb.CreateString(out var sp1));
      TableE.EndTableE(bb);
      sp1.Fill("Hello World");

      var t = TableE.GetRootAsTableE(bb.ByteBuffer);

      "Hello World".Should().IsSameOrEqualTo(t.X);

      var utf8 = new BigSpan<byte>(Encoding.UTF8.GetBytes("Hello World"));
      var xSpan = t.GetXSpan();
      for (var i = 0u; i < 11; ++i)
        utf8[i].Should().IsSameOrEqualTo(xSpan[i]);
      ((byte)0).Should().IsSameOrEqualTo(xSpan[11u]);
    }

    [Test]
    public static void TableFTest()
    {
      var bb = new BigBufferBuilder();
      TableF.StartTableF(bb);
      TableF.AddX(bb, TableF.CreateXVector(bb, out var x));
      TableF.EndTableF(bb);
      x.Fill(new[] { "Hello", "World" });

      var t = TableF.GetRootAsTableF(bb.ByteBuffer);

      "Hello".Should().IsSameOrEqualTo(t.X(0));
      "World".Should().IsSameOrEqualTo(t.X(1));
    }

    [Test]
    public static void TableGTest()
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

      t.X(0).Should().NotBeNull();
      t.X(0).Should().NotBeNull();
      t.X(1).Should().NotBeNull();
      t.X(2).Should().NotBeNull();
      t.X(3).Should().NotBeNull();

      (t.X(0)?.X).Should().BeTrue();
      (t.X(1)?.X).Should().BeFalse();
      (t.X(2)?.X).Should().BeTrue();
      (t.X(3)?.X).Should().BeFalse();
    }

    [Test]
    public static void TableHTest()
    {
      var bb = new BigBufferBuilder();
      TableH.StartTableH(bb);
      TableH.AddX(bb, TableH.CreateXVector(bb, out var x));
      TableH.EndTableH(bb);
      x.Fill(new[] { true, false, true, false });

      var t = TableH.GetRootAsTableH(bb.ByteBuffer);

      t.X(0).Should().BeTrue();
      t.X(1).Should().BeFalse();
      t.X(2).Should().BeTrue();
      t.X(3).Should().BeFalse();

    }

    [Test]
    public static void TableITest()
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

      t.X(0).Should().NotBeNull();
      t.X(1).Should().NotBeNull();
      t.X(2).Should().NotBeNull();
      t.X(3).Should().NotBeNull();

      t.X(0)!.Value.X(0).Should().BeTrue();
      t.X(0)!.Value.X(1).Should().BeFalse();

      t.X(1)!.Value.X(0).Should().BeTrue();
      t.X(1)!.Value.X(1).Should().BeTrue();

      t.X(2)!.Value.X(0).Should().BeFalse();
      t.X(2)!.Value.X(1).Should().BeFalse();

      t.X(3)!.Value.X(0).Should().BeFalse();
      t.X(3)!.Value.X(1).Should().BeTrue();
    }

    [Test]
    public static void TableJTest()
    {
      var bb = new BigBufferBuilder();

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var x));
      var to = TableJ.EndTableJ(bb);
      x.Fill(new[] { to });

      var t = TableJ.GetRootAsTableJ(bb.ByteBuffer);

      var t2 = t.X(0);
      t2.Should().NotBeNull();
      var t3 = t2!.Value.X(0);
      t3.Should().NotBeNull();
    }

    [Test]
    public static void TableKTest()
    {
      var bb = new BigBufferBuilder();

      TableK.StartTableK(bb);
      TableK.AddX(bb, bb.CreateVector(out var kx));
      var ko = TableK.EndTableK(bb);
      0.Should().IsSameOrEqualTo(ko.Value);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });
      kx.Fill(new[] { jo });

      var t = TableK.GetRootAsTableK(bb.ByteBuffer);
      ko.Value.Should().IsSameOrEqualTo(t._model.Offset);

      var j1 = t.X(0);
      j1.Should().NotBeNull();
      var jv1 = j1!.Value;
      jo.Value.Should().IsSameOrEqualTo(jv1._model.Offset);
      1.Should().IsSameOrEqualTo(jv1.XLength);

      var j2 = jv1.X(0);
      j2.Should().NotBeNull();
      var jv2 = j2!.Value;
      jo.Value.Should().IsSameOrEqualTo(jv2._model.Offset);
      1.Should().IsSameOrEqualTo(jv2.XLength);

      var j3 = jv2.X(0);
      j3.Should().NotBeNull();
      var jv3 = j3!.Value;
      jo.Value.Should().IsSameOrEqualTo(jv3._model.Offset);
      1.Should().IsSameOrEqualTo(jv3.XLength);
    }

    [Test]
    public static void TableLTest()
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
      t2.Should().NotBeNull();
      var t3 = t2!.Value.X(0);
      t3.Should().NotBeNull();
      var t4 = t3!.Value.X(0);
      t4.Should().NotBeNull();
    }


    [Test]
    public static void TableNTest()
    {
      var bb = new BigBufferBuilder();
      TableN.StartTableN(bb);
      TableN.AddX(bb, bb.CreateOffset<TableJ>(out var nx).Value);
      TableN.AddXType(bb, UnionM.TableJ);
      var no = TableN.EndTableN(bb);
      0.Should().IsSameOrEqualTo(no.Value);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });
      nx.Fill(jo);

      var t = TableN.GetRootAsTableN(bb.ByteBuffer);
      no.Value.Should().IsSameOrEqualTo(t._model.Offset);

      UnionM.TableJ.Should().IsSameOrEqualTo(t.XType);

      var t2 = t.X<TableJ>();
      t2.Should().NotBeNull();
      var t3 = t2!.Value.X(0);
      t3.Should().NotBeNull();
      var t4 = t3!.Value.X(0);
      t4.Should().NotBeNull();
    }


    [Test]
    public static void TablePTest()
    {
      var bb = new BigBufferBuilder();
      TableP.StartTableP(bb);
      TableP.AddX(bb, EnumO.z);
      TableP.EndTableP(bb);

      var t = TableP.GetRootAsTableP(bb.ByteBuffer);
      EnumO.z.Should().IsSameOrEqualTo(t.X);

      t.X = EnumO.y;
      EnumO.y.Should().IsSameOrEqualTo(t.X);

      t.X = EnumO.x;
      EnumO.x.Should().IsSameOrEqualTo(t.X);
    }

    [Test]
    public static void TableQTest()
    {
#if DEBUG
      BigBufferBuilder.UseExistingVTables = false;
#endif

      var bb = new BigBufferBuilder();
      TableQ.StartTableQ(bb);
      TableQ.AddX(bb, bb.CreateOffset<TableN>(out var qx));
      TableQ.AddY(bb, bb.CreateOffset<TableP>(out var qy));
      TableQ.AddZ(bb, bb.CreateOffset<TableJ>(out var qz).Value);
      TableQ.AddZType(bb, UnionM.TableJ);
      var qo = TableQ.EndTableQ(bb);
      0.Should().IsSameOrEqualTo(qo.Value);

      TableN.StartTableN(bb);
      TableN.AddX(bb, bb.CreateOffset<TableJ>(out var nx).Value);
      TableN.AddXType(bb, UnionM.TableJ);
      var no = TableN.EndTableN(bb);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);

      jx.Fill(new[] { jo });
      nx.Fill(jo);
      TableP.StartTableP(bb);
      TableP.AddX(bb, EnumO.z);
      var po = TableP.EndTableP(bb);

      qx.Fill(no);
      qy.Fill(po);
      qz.Fill(jo);

      var t = TableQ.GetRootAsTableQ(bb.ByteBuffer);

      qo.Value.Should().IsSameOrEqualTo(t._model.Offset);

      var n = t.X;
      n.Should().NotBeNull();

      var nv = n!.Value;
      no.Value.Should().IsSameOrEqualTo(nv._model.Offset);

      UnionM.TableJ.Should().IsSameOrEqualTo(nv.XType);

      var j1 = nv.X<TableJ>();
      j1.Should().NotBeNull();

      var jv1 = j1!.Value;
      jo.Value.Should().IsSameOrEqualTo(jv1._model.Offset);

      var p = t.Y;
      p.Should().NotBeNull();

      var pv = p!.Value;
      po.Value.Should().IsSameOrEqualTo(pv._model.Offset);
      EnumO.z.Should().IsSameOrEqualTo(pv.X);

      UnionM.TableJ.Should().IsSameOrEqualTo(t.ZType);

      var j2 = t.Z<TableJ>();
      j2.Should().NotBeNull();

      var jv2 = j2!.Value;
      jo.Value.Should().IsSameOrEqualTo(jv2._model.Offset);

      jo.Value.Should().IsSameOrEqualTo(jv1._model.Offset);
      jo.Value.Should().IsSameOrEqualTo(jv2._model.Offset);

      jv1.Should().IsSameOrEqualTo(jv2);

    }
  }
}
