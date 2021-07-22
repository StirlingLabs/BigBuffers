using NUnit.Framework;

namespace BigBuffers.Tests
{
  public class BigBufferBuilderTests
  {
    
    [Test]
    public void TestEmptyVTable()
    {
      var builder = new BigBufferBuilder();
      builder.StartTable(0);
      CollectionAssert.AreEqual(new byte[]
      {
        // table:
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0
      }, builder.ByteBuffer.ToFullArray());
      builder.EndTable();
      CollectionAssert.AreEqual(new byte[]
        {
          // table:
          // signed offset to vtable (-8)
          0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
          
          // vtable:
          // size of vtable (4)
          4, 0,
          // size of table (8)
          8, 0
        },
        builder.SizedByteArray());
    }

    [Test]
    public void TestVTableWithOneBool()
    {
      var builder = new BigBufferBuilder();
      builder.StartTable(1);
      CollectionAssert.AreEqual(new byte[]
      {
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0
      }, builder.ByteBuffer.ToFullArray());
      builder.Add(0,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        // table:
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0,
        1, // one byte boolean field: true
      }, builder.SizedByteArray());
      builder.EndTable();
      CollectionAssert.AreEqual(new byte[]
        {
          // table:
          // signed offset to vtable (-16)
          0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
          
          1, // one byte boolean field: true
          0, 0, 0, 0, 0, 0, 0, //padding to 8-byte alignment of table
          
          // vtable:
          // size of vtable (4)
          6, 0,
          // size of table (16)
          16, 0,
          8, 0, // offset to bool field (1)
        },
        builder.SizedByteArray());
    }

    [Test]
    public void TestVTableWithTwoBools()
    {
      var builder = new BigBufferBuilder();
      builder.StartTable(2);
      CollectionAssert.AreEqual(new byte[]
      {
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0
      }, builder.ByteBuffer.ToFullArray());
      builder.Add(0,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        // table:
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0,
        1, // one byte boolean field: true
      }, builder.SizedByteArraySegment());
      builder.Add(1,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        // table:
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, // 2 one byte boolean fields: true, true
      }, builder.SizedByteArraySegment());
      builder.EndTable();
      CollectionAssert.AreEqual(new byte[]
        {
          // table:
          // signed offset to vtable (-16)
          0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
          
          1, // one byte boolean field: true
          1, // one byte boolean field: true
          0, 0, 0, 0, 0, 0, //padding to 8-byte alignment of table
          
          // vtable:
          // size of vtable (8)
          8, 0,
          // size of table (16)
          16, 0,
          8, 0, // offset to bool field (1)
          9, 0, // offset to bool field (2)
        },
        builder.SizedByteArraySegment());
    }

    [Test]
    public void TestVTableWith8Bools()
    {
      var builder = new BigBufferBuilder();
      builder.StartTable(8);
      CollectionAssert.AreEqual(new byte[]
      {
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0
      }, builder.ByteBuffer.ToFullArray());
      builder.Add(0,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        // table:
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0,
        1, // one byte boolean field: true
      }, builder.SizedByteArraySegment());
      builder.Add(1,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        // table:
        // placeholder for signed offset to vtable
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, // 2 one byte boolean fields: true, true
      }, builder.SizedByteArraySegment());
      builder.Add(2,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1,
      }, builder.SizedByteArraySegment());
      builder.Add(3,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1,
      }, builder.SizedByteArraySegment());
      builder.Add(4,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1,
      }, builder.SizedByteArraySegment());
      builder.Add(5,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1,
      }, builder.SizedByteArraySegment());
      builder.Add(6,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1,
      }, builder.SizedByteArraySegment());
      builder.Add(7,true, false);
      CollectionAssert.AreEqual(new byte[]
      {
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1, 1,
      }, builder.SizedByteArraySegment());
      builder.EndTable();
      CollectionAssert.AreEqual(new byte[]
        {
          // table:
          // signed offset to vtable (-16)
          0xF0, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
          
          // 8 one byte boolean fields, all true
          1, 1, 1, 1, 1, 1, 1, 1,
          
          // vtable:
          // size of vtable (20)
          20, 0,
          // size of table (16)
          16, 0,
          8, 0, // offset to bool field (1),
          9, 0, // offset to bool field (2),
          10, 0, // offset to bool field (3),
          11, 0, // offset to bool field (4),
          12, 0, // offset to bool field (5),
          13, 0, // offset to bool field (6),
          14, 0, // offset to bool field (7),
          15, 0, // offset to bool field (8)
        },
        builder.SizedByteArraySegment());
    }
  }
}
