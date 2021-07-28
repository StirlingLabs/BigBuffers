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

      h.Value.Should().Be(0);
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

      bb.Offset.Should().Be(8 + 32 + 3 * 2);

      var t = Generated.Test.GetRootAsTest(bb.ByteBuffer);

      t._model.Offset.Should().Be(0);

      t.Hash.Should().NotBeNull();

      var h = t.Hash!.Value;

      for (var i = 0u; i < 32; ++i)
        h.Bytes(i).Should().Be(initHashBytes[i]);

      var s = h.Bytes();

      for (var i = 0u; i < 32; ++i)
        s[i].Should().Be(initHashBytes[i]);

      s.AsSmallSlices(RandomNumberGenerator.Fill);

      s[1u] = 0;

      s.AsPinnedEnumerable(ps =>
        CollectionAssert.AreNotEqual(initHashBytes, ps));

      for (var i = 0u; i < 32; ++i)
        h.Bytes(i).Should().Be(s[i]);
    }

    [Test]
    public static void TableATest()
    {
      var bb = new BigBufferBuilder();
      TableA.StartTableA(bb);
      TableA.AddX(bb, 0x0102030405060708uL);
      TableA.EndTableA(bb);

      var t = TableA.GetRootAsTableA(bb.ByteBuffer);

      t.X.Should().Be(0x0102030405060708uL);
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

      t.X!.Value.GetX().Should().Be(0x0102030405060708uL);

      t.X.Value.X = 0x0807060504030201uL;

      t.X.Value.X.Should().Be(0x0807060504030201uL);

      t.X.Value.SetX(0x0102030405060708uL);

      t.X.Value.X.Should().Be(0x0102030405060708uL);
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

      t.X.Should().Be(0x0102030405060708uL);
    }

    [Test]
    public static void TableETest()
    {
      var bb = new BigBufferBuilder();
      TableE.StartTableE(bb);
      TableE.AddX(bb, bb.CreateString(out var sp1));
      TableE.EndTableE(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      sp1.Fill("Hello World");

      Placeholder.ValidateAllFilled(bb);

      var t = TableE.GetRootAsTableE(bb.ByteBuffer);

      t.X.Should().Be("Hello World");

      var utf8 = new BigSpan<byte>(Encoding.UTF8.GetBytes("Hello World"));
      var xSpan = t.GetXSpan();
      for (var i = 0u; i < 11; ++i)
        xSpan[i].Should().Be(utf8[i]);
      xSpan[11u].Should().Be((byte)0);
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

      t.X(0).Should().Be("Hello");
      t.X(1).Should().Be("World");
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

      var x0 = t.X(0)?.X;
      x0.Should().BeTrue();
      
      var x1 = t.X(1)?.X;
      x1.Should().BeFalse();
      
      var x2 = t.X(2)?.X;
      x2.Should().BeTrue();

      var x3 = t.X(3)?.X;
      x3.Should().BeFalse();
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

      ValidateAllPlaceholdersNotFilled(bb);

      x.Fill(new[] { to });

      Placeholder.ValidateAllFilled(bb);

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
      ko.Value.Should().Be(0);

      ValidateAllPlaceholdersNotFilled(bb);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });
      kx.Fill(new[] { jo });

      Placeholder.ValidateAllFilled(bb);

      var t = TableK.GetRootAsTableK(bb.ByteBuffer);
      t._model.Offset.Should().Be(ko.Value);

      var j1 = t.X(0);
      j1.Should().NotBeNull();
      var jv1 = j1!.Value;
      jv1._model.Offset.Should().Be(jo.Value);
      jv1.XLength.Should().Be(1);

      var j2 = jv1.X(0);
      j2.Should().NotBeNull();
      var jv2 = j2!.Value;
      jv2._model.Offset.Should().Be(jo.Value);
      jv2.XLength.Should().Be(1);

      var j3 = jv2.X(0);
      j3.Should().NotBeNull();
      var jv3 = j3!.Value;
      jv3._model.Offset.Should().Be(jo.Value);
      jv3.XLength.Should().Be(1);
    }

    [Test]
    public static void TableLTest()
    {
      var bb = new BigBufferBuilder();

      TableL.StartTableL(bb);
      TableL.AddX(bb, bb.CreateOffset<TableK>(out var lx));
      TableL.EndTableL(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      TableK.StartTableK(bb);
      TableK.AddX(bb, bb.CreateVector(out var kx));
      var ko = TableK.EndTableK(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });
      kx.Fill(new[] { jo });
      lx.Fill(ko);

      Placeholder.ValidateAllFilled(bb);

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
      no.Value.Should().Be(0);

      ValidateAllPlaceholdersNotFilled(bb);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });
      nx.Fill(jo);

      Placeholder.ValidateAllFilled(bb);

      var t = TableN.GetRootAsTableN(bb.ByteBuffer);
      t._model.Offset.Should().Be(no.Value);

      t.XType.Should().Be(UnionM.TableJ);

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
      t.X.Should().Be(EnumO.z);

      t.X = EnumO.y;
      t.X.Should().Be(EnumO.y);

      t.X = EnumO.x;
      t.X.Should().Be(EnumO.x);
    }

    [Test]
    public static void TableQTest()
    {

      var bb = new BigBufferBuilder();
      TableQ.StartTableQ(bb);
      TableQ.AddX(bb, bb.CreateOffset<TableN>(out var qx));
      TableQ.AddY(bb, bb.CreateOffset<TableP>(out var qy));
      TableQ.AddZ(bb, bb.CreateOffset<TableJ>(out var qz).Value);
      TableQ.AddZType(bb, UnionM.TableJ);
      var qo = TableQ.EndTableQ(bb);
      qo.Value.Should().Be(0);

      ValidateAllPlaceholdersNotFilled(bb);

      TableN.StartTableN(bb);
      TableN.AddX(bb, bb.CreateOffset<TableJ>(out var nx).Value);
      TableN.AddXType(bb, UnionM.TableJ);
      var no = TableN.EndTableN(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      jx.Fill(new[] { jo });
      nx.Fill(jo);
      TableP.StartTableP(bb);
      TableP.AddX(bb, EnumO.z);
      var po = TableP.EndTableP(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      qx.Fill(no);
      qy.Fill(po);
      qz.Fill(jo);

      Placeholder.ValidateAllFilled(bb);

      var t = TableQ.GetRootAsTableQ(bb.ByteBuffer);

      t._model.Offset.Should().Be(qo.Value);

      var n = t.X;
      n.Should().NotBeNull();

      var nv = n!.Value;
      nv._model.Offset.Should().Be(no.Value);

      nv.XType.Should().Be(UnionM.TableJ);

      var j1 = nv.X<TableJ>();
      j1.Should().NotBeNull();

      var jv1 = j1!.Value;
      jv1._model.Offset.Should().Be(jo.Value);

      var p = t.Y;
      p.Should().NotBeNull();

      var pv = p!.Value;
      pv._model.Offset.Should().Be(po.Value);
      pv.X.Should().Be(EnumO.z);

      t.ZType.Should().Be(UnionM.TableJ);

      var j2 = t.Z<TableJ>();
      j2.Should().NotBeNull();

      var jv2 = j2!.Value;
      jv2._model.Offset.Should().Be(jo.Value);

      jv1._model.Offset.Should().Be(jo.Value);
      jv2._model.Offset.Should().Be(jo.Value);

      jv2.Should().Be(jv1);

    }


    [Test]
    public static void TableRTest()
    {
      var bb = new BigBufferBuilder();
      TableR.StartTableR(bb);
      TableR.AddX(bb, bb.CreateVector(out var rx));
      TableR.AddXType(bb, bb.CreateVector(out var rxt));
      var ro = TableR.EndTableR(bb);

      ValidateAllPlaceholdersNotFilled(bb);

      TableJ.StartTableJ(bb);
      TableJ.AddX(bb, bb.CreateVector(out var jx));
      var jo = TableJ.EndTableJ(bb);
      jx.Fill(new[] { jo });

      ValidateAllPlaceholdersNotFilled(bb);

      TableK.StartTableK(bb);
      TableK.AddX(bb, bb.CreateVector(out var kx));
      var ko = TableK.EndTableK(bb);
      kx.Fill(new[] { jo });

      ValidateAllPlaceholdersNotFilled(bb);

      rxt.Fill(new[] { UnionM.TableK, UnionM.TableJ });
      rx.Fill(new Offset[] { ko, jo });

      Placeholder.ValidateAllFilled(bb);

      var t = TableR.GetRootAsTableR(bb.ByteBuffer);
      t._model.Offset.Should().Be(ro.Value);

      t.XType(0).Should().BeOfType<UnionM>().And.Be(UnionM.TableK);

      t.XType(1).Should().BeOfType<UnionM>().And.Be(UnionM.TableJ);

      var k = t.X<TableK>(0);
      k.Should().NotBeNull();
      k!.Value._model.Offset.Should().Be(ko.Value);

      var j = t.X<TableJ>(1);
      j.Should().NotBeNull();
      j!.Value._model.Offset.Should().Be(jo.Value);

    }
    
    [Conditional("DEBUG")]
    private static void ValidateAllPlaceholdersNotFilled(BigBufferBuilder bb)
      => Assert.Throws<PlaceholdersUnfilledException>(() => Placeholder.ValidateAllFilled(bb));
  }
}
