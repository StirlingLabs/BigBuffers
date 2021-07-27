using System;
using FluentAssertions;
using NUnit.Framework;

namespace BigBuffers.Test
{
  public class ByteBufferTests
  {
    [Test]
    public void ByteBuffer_Length_MatchesBufferLength()
    {
      var buffer = new byte[1000];
      var uut = new ByteBuffer(buffer);
      (uut.LongLength).Should().Be((ulong)buffer.LongLength);
    }

    [Test]
    public void ByteBuffer_PutBytePopulatesBufferAtZeroOffset()
    {
      var buffer = new byte[1];
      var uut = new ByteBuffer(buffer);
      uut.Put<byte>(0, 99);

      (buffer[0]).Should().Be(((byte)99));
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_PutByteCannotPutAtOffsetPastLength()
    {
      var uut = new ByteBuffer(1);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put<byte>(1, 99));
    }
#endif

    [Test]
    public void ByteBuffer_PutShortPopulatesBufferCorrectly()
    {
      var buffer = new byte[2];
      var uut = new ByteBuffer(buffer);
      uut.Put(0, (short)1);

      // Ensure Endianness was written correctly
      (buffer[0]).Should().Be(((byte)1));
      (buffer[1]).Should().Be(((byte)0));
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_PutShortCannotPutAtOffsetPastLength()
    {
      var uut = new ByteBuffer(2);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put<short>(2, 99));
    }
#endif

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_PutShortChecksLength()
    {
      var uut = new ByteBuffer(1);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put<short>(0, 99));
    }

    [Test]
    public void ByteBuffer_PutShortChecksLengthAndOffset()
    {
      var uut = new ByteBuffer(2);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put<short>(1, 99));
    }
#endif

    [Test]
    public void ByteBuffer_PutIntPopulatesBufferCorrectly()
    {
      var buffer = new byte[4];
      var uut = new ByteBuffer(buffer);
      uut.Put(0, 0x0A0B0C0D);

      // Ensure Endianness was written correctly
      (buffer[0]).Should().Be(0x0D);
      (buffer[1]).Should().Be(0x0C);
      (buffer[2]).Should().Be(0x0B);
      (buffer[3]).Should().Be(0x0A);
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_PutIntCannotPutAtOffsetPastLength()
    {
      var uut = new ByteBuffer(4);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put(2, 0x0A0B0C0D));
    }

    [Test]
    public void ByteBuffer_PutIntChecksLength()
    {
      var uut = new ByteBuffer(1);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put(0, 0x0A0B0C0D));
    }

    [Test]
    public void ByteBuffer_PutIntChecksLengthAndOffset()
    {
      var uut = new ByteBuffer(4);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put(2, 0x0A0B0C0D));
    }
#endif

    [Test]
    public void ByteBuffer_PutLongPopulatesBufferCorrectly()
    {
      var buffer = new byte[8];
      var uut = new ByteBuffer(buffer);
      uut.Put(0, 0x010203040A0B0C0D);

      // Ensure Endianness was written correctly
      (buffer[0]).Should().Be(0x0D);
      (buffer[1]).Should().Be(0x0C);
      (buffer[2]).Should().Be(0x0B);
      (buffer[3]).Should().Be(0x0A);
      (buffer[4]).Should().Be(0x04);
      (buffer[5]).Should().Be(0x03);
      (buffer[6]).Should().Be(0x02);
      (buffer[7]).Should().Be(0x01);
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_PutLongCannotPutAtOffsetPastLength()
    {
      var uut = new ByteBuffer(8);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put(2, 0x010203040A0B0C0D));
    }

    [Test]
    public void ByteBuffer_PutLongChecksLength()
    {
      var uut = new ByteBuffer(1);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put(0, 0x010203040A0B0C0D));
    }

    [Test]
    public void ByteBuffer_PutLongChecksLengthAndOffset()
    {
      var uut = new ByteBuffer(8);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Put(2, 0x010203040A0B0C0D));
    }
#endif

    [Test]
    public void ByteBuffer_GetByteReturnsCorrectData()
    {
      var buffer = new byte[1];
      buffer[0] = 99;
      var uut = new ByteBuffer(buffer);
      (uut.GetByte(0)).Should().Be(((byte)99));
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_GetByteChecksOffset()
    {
      var uut = new ByteBuffer(1);
      Assert.Throws<IndexOutOfRangeException>(() => uut.GetByte(1));
    }
#endif

    [Test]
    public void ByteBuffer_GetShortReturnsCorrectData()
    {
      var buffer = new byte[2];
      buffer[0] = 1;
      buffer[1] = 0;
      var uut = new ByteBuffer(buffer);
      (1).Should().Be(uut.Get<short>(0));
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_GetShortChecksOffset()
    {
      var uut = new ByteBuffer(2);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Get<short>(2));
    }

    [Test]
    public void ByteBuffer_GetShortChecksLength()
    {
      var uut = new ByteBuffer(2);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Get<short>(1));
    }
#endif

    [Test]
    public void ByteBuffer_GetIntReturnsCorrectData()
    {
      var buffer = new byte[4];
      buffer[0] = 0x0D;
      buffer[1] = 0x0C;
      buffer[2] = 0x0B;
      buffer[3] = 0x0A;
      var uut = new ByteBuffer(buffer);
      (uut.Get<int>(0)).Should().Be(0x0A0B0C0D);
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_GetIntChecksOffset()
    {
      var uut = new ByteBuffer(4);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Get<int>(4));
    }

    [Test]
    public void ByteBuffer_GetIntChecksLength()
    {
      var uut = new ByteBuffer(2);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Get<int>(0));
    }
#endif

    [Test]
    public void ByteBuffer_GetLongReturnsCorrectData()
    {
      var buffer = new byte[8];
      buffer[0] = 0x0D;
      buffer[1] = 0x0C;
      buffer[2] = 0x0B;
      buffer[3] = 0x0A;
      buffer[4] = 0x04;
      buffer[5] = 0x03;
      buffer[6] = 0x02;
      buffer[7] = 0x01;
      var uut = new ByteBuffer(buffer);
      (uut.Get<long>(0)).Should().Be(0x010203040A0B0C0D);
    }

#if !BYTEBUFFER_NO_BOUNDS_CHECK
    [Test]
    public void ByteBuffer_GetLongChecksOffset()
    {
      var uut = new ByteBuffer(8);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Get<long>(8));
    }

    [Test]
    public void ByteBuffer_GetLongChecksLength()
    {
      var uut = new ByteBuffer(7);
      Assert.Throws<ArgumentOutOfRangeException>(() => uut.Get<long>(0));
    }
#endif

    [Test]
    public void ByteBuffer_ToFullArray_MatchesBuffer()
    {
      var buffer = new byte[4];
      buffer[0] = 0x0D;
      buffer[1] = 0x0C;
      buffer[2] = 0x0B;
      buffer[3] = 0x0A;
      var uut = new ByteBuffer(buffer);
      CollectionAssert.AreEqual(buffer, uut.ToFullArray());
    }

    [Test]
    public void ByteBuffer_ToSizedArray_MatchesBuffer()
    {
      var buffer = new byte[4];
      buffer[0] = 0x0D;
      buffer[1] = 0x0C;
      buffer[2] = 0x0B;
      buffer[3] = 0x0A;
      var uut = new ByteBuffer(buffer);
      CollectionAssert.AreEqual(buffer, uut.ToFullArray());
    }

    [Test]
    public void ByteBuffer_Duplicate_MatchesBuffer()
    {
      var buffer = new byte[4];
      buffer[0] = 0x0D;
      buffer[1] = 0x0C;
      buffer[2] = 0x0B;
      buffer[3] = 0x0A;
      var uut = new ByteBuffer(buffer);
      (uut.Get<int>(0)).Should().Be(0x0A0B0C0D);

      // Advance by two bytes
      uut.Position = 2;
      uut = uut.Duplicate();
      (uut.Get<short>(2)).Should().Be(0x0A0B);

      // Advance by one more byte
      uut.Position = 1;
      uut = uut.Duplicate();
      (uut.GetByte(3)).Should().Be(0x0A);
    }

    [Test]
    public void ByteBuffer_To_Array_Float()
    {
      const int len = 9;

      // Construct the data array
      var fData = new float[len];
      fData[0] = 1.0079F;
      fData[1] = 4.0026F;
      fData[2] = 6.941F;
      fData[3] = 9.0122F;
      fData[4] = 10.811F;
      fData[5] = 12.0107F;
      fData[6] = 14.0067F;
      fData[7] = 15.9994F;
      fData[8] = 18.9984F;

      // Tranfer it to a byte array
      var buffer = new byte[sizeof(float) * fData.Length];
      Buffer.BlockCopy(fData, 0, buffer, 0, buffer.Length);

      // Create the Byte Buffer from byte array
      var uut = new ByteBuffer(buffer);

      // Get the full array back out and ensure they are equivalent
      var bbArray = uut.ToArray<float>(0, len);
      CollectionAssert.AreEqual(fData, bbArray);

      // Get a portion of the full array back out and ensure the
      // subrange agrees
      var bbArray2 = uut.ToArray<float>(4, len - 1);
      (len - 1).Should().Be(bbArray2.Length);
      for (var i = 1; i < len - 1; i++)
      {
        (bbArray2[i - 1]).Should().Be(fData[i]);
      }

      // Get a sub portion of the full array back out and ensure the
      // subrange agrees
      var bbArray3 = uut.ToArray<float>(8, len - 4);
      (len - 4).Should().Be(bbArray3.Length);
      for (var i = 2; i < len - 4; i++)
      {
        (bbArray3[i - 2]).Should().Be(fData[i]);
      }
    }

    public void ByteBuffer_Put_Array_Helper<T>(T[] data, ulong typeSize)
      where T : unmanaged
    {
      // Create the Byte Buffer
      var uut = new ByteBuffer(1024);

      // Put the data into the buffer and make sure the offset is
      // calculated correctly
      var nSize = uut.Put(0, data);
      nSize.Should().Be(typeSize * (ulong)data.LongLength);

      // Get the full array back out and ensure they are equivalent
      var bbArray = uut.ToArray<T>(0, (ulong)data.LongLength);
      CollectionAssert.AreEqual(data, bbArray);
    }

    [Test]
    public void ByteBuffer_Put_Array_Float()
    {
      const int len = 9;

      // Construct the data array
      var data = new float[len];
      data[0] = 1.0079F;
      data[1] = 4.0026F;
      data[2] = 6.941F;
      data[3] = 9.0122F;
      data[4] = 10.811F;
      data[5] = 12.0107F;
      data[6] = 14.0067F;
      data[7] = 15.9994F;
      data[8] = 18.9984F;

      ByteBuffer_Put_Array_Helper(data, sizeof(float));
    }

    [Test]
    public void ByteBuffer_Put_Array_Double()
    {
      const int len = 9;

      // Construct the data array
      var data = new double[len];
      data[0] = 1.0079;
      data[1] = 4.0026;
      data[2] = 6.941;
      data[3] = 9.0122;
      data[4] = 10.811;
      data[5] = 12.0107;
      data[6] = 14.0067;
      data[7] = 15.9994;
      data[8] = 18.9984;

      ByteBuffer_Put_Array_Helper(data, sizeof(double));
    }

    [Test]
    public void ByteBuffer_Put_Array_Int()
    {
      const int len = 9;

      // Construct the data array
      var data = new int[len];
      data[0] = 1;
      data[1] = 4;
      data[2] = 6;
      data[3] = 9;
      data[4] = 10;
      data[5] = 12;
      data[6] = 14;
      data[7] = 15;
      data[8] = 18;

      ByteBuffer_Put_Array_Helper(data, sizeof(int));
    }


    [Test]
    public void ByteBuffer_Put_Array_UInt()
    {
      // Construct the data array
      var data = new uint[]
      {
        1, 4, 6, 9, 10, 12, 14, 15, 18
      };

      ByteBuffer_Put_Array_Helper(data, sizeof(uint));
    }

    [Test]
    public void ByteBuffer_Put_Array_Bool()
    {
      // Construct the data array
      var data = new[]
      {
        true, true, false, true,
        false, true, true, true,
        false
      };

      ByteBuffer_Put_Array_Helper(data, sizeof(bool));
    }

    [Test]
    public void ByteBuffer_Put_Array_Long()
    {
      const int len = 9;

      // Construct the data array
      var data = new long[len];
      data[0] = 1;
      data[1] = 4;
      data[2] = 6;
      data[3] = 9;
      data[4] = 10;
      data[5] = 12;
      data[6] = 14;
      data[7] = 15;
      data[8] = 18;

      ByteBuffer_Put_Array_Helper(data, sizeof(long));
    }

    [Test]
    public void ByteBuffer_Put_Array_Byte()
    {
      const int len = 9;

      // Construct the data array
      var data = new byte[len];
      data[0] = 1;
      data[1] = 4;
      data[2] = 6;
      data[3] = 9;
      data[4] = 10;
      data[5] = 12;
      data[6] = 14;
      data[7] = 15;
      data[8] = 18;

      ByteBuffer_Put_Array_Helper(data, sizeof(byte));
    }

    [Test]
    public void ByteBuffer_Put_Array_SByte()
    {
      const int len = 9;

      // Construct the data array
      var data = new sbyte[len];
      data[0] = 1;
      data[1] = 4;
      data[2] = 6;
      data[3] = 9;
      data[4] = 10;
      data[5] = 12;
      data[6] = 14;
      data[7] = 15;
      data[8] = 18;

      ByteBuffer_Put_Array_Helper(data, sizeof(sbyte));
    }

    [Test]
    public void ByteBuffer_Put_Array_Null_Throws()
    {
      // Create the Byte Buffer
      var uut = new ByteBuffer(1024);

      // create a null array and try to put it into the buffer
      Assert.Throws<ArgumentNullException>(() => uut.Put(1024, (float[])null));
    }

    [Test]
    public void ByteBuffer_Put_Array_Empty_Throws()
    {
      // Create the Byte Buffer
      var uut = new ByteBuffer(1024);

      // create an array of length == 0, and try to put it into the buffer
      var data = new float[0];
      Assert.Throws<ArgumentException>(() => uut.Put(1024, data));
    }

#pragma warning disable 169, 649
    // These are purposely not used and the warning is suppress
    private struct DummyStruct
    {
      public int A;
      public float B;
    }
#pragma warning restore 169, 649

    [Test]
    public void ByteBuffer_Put_Array_Structs()
    {
      // Create the Byte Buffer
      var uut = new ByteBuffer(1024);

      // Create an array of dummy structures that shouldn't be
      // able to be put into the buffer
      var data = new DummyStruct[10];
      uut.Put(0, data);
    }

    [Test]
    public void ByteBuffer_Get_Double()
    {
      var uut = new ByteBuffer(1024);
      const double value = 3.14159265;
      uut.Put(900, value);
      var getValue = uut.Get<double>(900);
      (getValue).Should().Be(value);
    }

    [Test]
    public void ByteBuffer_Get_Float()
    {
      var uut = new ByteBuffer(1024);
      var value = 3.14159265F;
      uut.Put(900, value);
      var getValue = uut.Get<float>(900);
      (getValue).Should().Be(value);
    }
  }
}
