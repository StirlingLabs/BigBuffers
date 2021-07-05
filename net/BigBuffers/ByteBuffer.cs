/*
 * Copyright 2014 Google Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// There are three conditional compilation symbols that have an impact on performance/features of this ByteBuffer implementation.
//
//      UNSAFE_BYTEBUFFER
//          This will use unsafe code to manipulate the underlying byte array. This
//          can yield a reasonable performance increase.
//
//      BYTEBUFFER_NO_BOUNDS_CHECK
//          This will disable the bounds check asserts to the byte array. This can
//          yield a small performance gain in normal code.
//
//      ENABLE_SPAN_T
//          This will enable reading and writing blocks of memory with a Span<T> instead of just
//          T[].  You can also enable writing directly to shared memory or other types of memory
//          by providing a custom implementation of ByteBufferAllocator.
//          ENABLE_SPAN_T also requires UNSAFE_BYTEBUFFER to be defined, or .NET
//          Standard 2.1.
//
// Using UNSAFE_BYTEBUFFER and BYTEBUFFER_NO_BOUNDS_CHECK together can yield a
// performance gain of ~15% for some operations, however doing so is potentially
// dangerous. Do so at your own risk!
//

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using StirlingLabs.Utilities;

#if ENABLE_SPAN_T && !UNSAFE_BYTEBUFFER && !NETSTANDARD2_1
#warning ENABLE_SPAN_T requires UNSAFE_BYTEBUFFER to also be defined
#endif

#if NETSTANDARD
using nuint = System.UIntPtr;
using nint = System.IntPtr;
#endif

namespace BigBuffers
{
    public abstract class ByteBufferAllocator
    {
#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public abstract BigSpan<byte> Span { get; }
        public abstract ReadOnlyBigSpan<byte> ReadOnlySpan { get; }

#else
        public byte[] Buffer
        {
            get;
            protected set;
        }
#endif

      public int Length => checked((int)LongLength);

      public long LongLength
      {
        get;
        protected set;
      }

        public abstract void GrowFront(long newSize);
    }

    public sealed class ByteArrayAllocator : ByteBufferAllocator
    {
        private byte[] _buffer;

        public ByteArrayAllocator(byte[] buffer)
        {
            _buffer = buffer;
            InitBuffer();
        }

        public override void GrowFront(long newSize)
        {
            if ((LongLength & 0xC0000000) != 0)
                throw new Exception(
                    "ByteBuffer: cannot grow buffer beyond 2 gigabytes.");

            if (newSize < LongLength)
                throw new Exception("ByteBuffer: cannot truncate buffer.");

            var newBuffer = new byte[newSize];
            _buffer.CopyTo(new BigSpan<byte>(newBuffer).Slice((nuint)(newSize - LongLength), (nuint)LongLength));
            //System.Buffer.BlockCopy(_buffer, 0, newBuffer, newSize - Length, Length);
            _buffer = newBuffer;
            InitBuffer();
        }

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public override BigSpan<byte> Span => (BigSpan<byte>)_buffer;
        public override ReadOnlyBigSpan<byte> ReadOnlySpan => (ReadOnlyBigSpan<byte>)_buffer;
#endif

        private void InitBuffer()
        {
            LongLength = _buffer.Length;
#if !ENABLE_SPAN_T
            Buffer = _buffer;
#endif
        }
    }

    /// <summary>
    /// Class to mimic Java's ByteBuffer which is used heavily in Flatbuffers.
    /// </summary>
    public class ByteBuffer
    {
        private ByteBufferAllocator _buffer;
        private long _pos;  // Must track start of the buffer.

        public ByteBuffer(ByteBufferAllocator allocator, long position)
        {
            _buffer = allocator;
            _pos = position;
        }

        public ByteBuffer(int size) : this(new byte[size]) { }

        public ByteBuffer(byte[] buffer) : this(buffer, 0) { }

        public ByteBuffer(byte[] buffer, long pos)
        {
            _buffer = new ByteArrayAllocator(buffer);
            _pos = pos;
        }

        public long Position
        {
            get => _pos;
            set => _pos = value;
        }

        public int Length => _buffer.Length;

        public long LongLength => _buffer.LongLength;

        public void Reset()
          => _pos = 0;

        // Create a new ByteBuffer on the same underlying data.
        // The new ByteBuffer's position will be same as this buffer's.
        public ByteBuffer Duplicate()
          => new ByteBuffer(_buffer, Position);

        // Increases the size of the ByteBuffer, and copies the old data towards
        // the end of the new buffer.
        public void GrowFront(long newSize)
          => _buffer.GrowFront(newSize);

        public byte[] ToArray(long pos, long len)
          => ToArray<byte>(pos, len);

        /// <summary>
        /// A lookup of type sizes. Used instead of Marshal.SizeOf() which has additional
        /// overhead, but also is compatible with generic functions for simplified code.
        /// </summary>
        private static readonly Dictionary<Type, int> genericSizes = new Dictionary<Type, int>
        {
            { typeof(bool),     sizeof(bool) },
            { typeof(float),    sizeof(float) },
            { typeof(double),   sizeof(double) },
            { typeof(sbyte),    sizeof(sbyte) },
            { typeof(byte),     sizeof(byte) },
            { typeof(short),    sizeof(short) },
            { typeof(ushort),   sizeof(ushort) },
            { typeof(int),      sizeof(int) },
            { typeof(uint),     sizeof(uint) },
            { typeof(ulong),    sizeof(ulong) },
            { typeof(long),     sizeof(long) },
        };

        /// <summary>
        /// Get the wire-size (in bytes) of a type supported by flatbuffers.
        /// </summary>
        /// <param name="t">The type to get the wire size of</param>
        /// <returns></returns>
        public static int SizeOf<T>()
          => genericSizes[typeof(T)];

        /// <summary>
        /// Checks if the Type provided is supported as scalar value
        /// </summary>
        /// <typeparam name="T">The Type to check</typeparam>
        /// <returns>True if the type is a scalar type that is supported, falsed otherwise</returns>
        public static bool IsSupportedType<T>()
          => genericSizes.ContainsKey(typeof(T));

        /// <summary>
        /// Get the wire-size (in bytes) of an typed array
        /// </summary>
        /// <typeparam name="T">The type of the array</typeparam>
        /// <param name="x">The array to get the size of</param>
        /// <returns>The number of bytes the array takes on wire</returns>
        public static long ArraySize<T>(T[] x)
          => SizeOf<T>() * x.LongLength;

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public static long ArraySize<T>(Span<T> x)
          => SizeOf<T>() * x.Length;
#endif

        // Get a portion of the buffer casted into an array of type T, given
        // the buffer position and length.
#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public T[] ToArray<T>(long pos, long len)
            where T : struct
        {
            AssertOffsetAndLength(pos, len);
            return _buffer.ReadOnlySpan.Slice((nuint)pos).CastAs<T>().Slice(0, (nuint)len).ToArray();
        }
#else
        public T[] ToArray<T>(int pos, int len)
            where T : struct
        {
            AssertOffsetAndLength(pos, len);
            T[] arr = new T[len];
            Buffer.BlockCopy(_buffer.Buffer, pos, arr, 0, ArraySize(arr));
            return arr;
        }
#endif

        public byte[] ToSizedArray()
          => ToArray<byte>(Position, LongLength - Position);

        public byte[] ToFullArray()
          => ToArray<byte>(0, LongLength);

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public BigSpan<byte> ToSpan(long pos, long len)
          => _buffer.Span.Slice((nuint)pos, (nuint)len);
#else
        public ArraySegment<byte> ToArraySegment(int pos, int len)
        {
            return new ArraySegment<byte>(_buffer.Buffer, pos, len);
        }

        public MemoryStream ToMemoryStream(int pos, int len)
        {
            return new MemoryStream(_buffer.Buffer, pos, len);
        }
#endif

#if !UNSAFE_BYTEBUFFER
        // A conversion union where all the members are overlapping. This allows to reinterpret the bytes of one type
        // as another, without additional copies.
        [StructLayout(LayoutKind.Explicit)]
        struct ConversionUnion
        {
          [FieldOffset(0)] public int intValue;
          [FieldOffset(0)] public float floatValue;
        }
#endif // !UNSAFE_BYTEBUFFER

        // Helper functions for the unsafe version.

#if !UNSAFE_BYTEBUFFER && (!ENABLE_SPAN_T || !NETSTANDARD2_1)
        // Helper functions for the safe (but slower) version.
        protected void WriteLittleEndian(long offset, long count, ulong data)
        {
            if (BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < count; i++)
                {
                    _buffer.Buffer[offset + i] = (byte)(data >> i * 8);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    _buffer.Buffer[offset + count - 1 - i] = (byte)(data >> i * 8);
                }
            }
        }

        protected ulong ReadLittleEndian(long offset, long count)
        {
            AssertOffsetAndLength(offset, count);
            ulong r = 0;
            if (BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < count; i++)
                {
                    r |= (ulong)_buffer.Buffer[offset + i] << i * 8;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    r |= (ulong)_buffer.Buffer[offset + count - 1 - i] << i * 8;
                }
            }
            return r;
        }
#elif ENABLE_SPAN_T && NETSTANDARD2_1
        protected void WriteLittleEndian(long offset, long count, ulong data)
        {
            if (BitConverter.IsLittleEndian)
              for (var i = 0; i < count; i++)
                _buffer.Span[(nuint)(offset + i)] = (byte)(data >> i * 8);
            else
              for (var i = 0; i < count; i++)
                _buffer.Span[(nuint)(offset + count - 1 - i)] = (byte)(data >> i * 8);
        }

        protected ulong ReadLittleEndian(long offset, long count)
        {
            AssertOffsetAndLength(offset, count);
            ulong r = 0;
            if (BitConverter.IsLittleEndian)
              for (var i = 0; i < count; i++)
                r |= (ulong)_buffer.Span[(nuint)(offset + i)] << i * 8;
            else
              for (var i = 0; i < count; i++)
                r |= (ulong)_buffer.Span[(nuint)(offset + count - 1 - i)] << i * 8;
            return r;
        }
#endif

        private void AssertOffsetAndLength(long offset, long length)
        {
#if !BYTEBUFFER_NO_BOUNDS_CHECK
            if (offset < 0 ||
                offset > _buffer.LongLength - length)
                throw new ArgumentOutOfRangeException(nameof(offset));
#endif
        }

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)

        public void PutSbyte(long offset, sbyte value)
        {
            AssertOffsetAndLength(offset, sizeof(sbyte));
            _buffer.Span[(nuint)offset] = (byte)value;
        }

        public void PutByte(long offset, byte value)
        {
            AssertOffsetAndLength(offset, sizeof(byte));
            _buffer.Span[(nuint)offset] = value;
        }

        public void PutByte(long offset, byte value, long count)
        {
            AssertOffsetAndLength(offset, sizeof(byte) * count);
            var span = _buffer.Span.Slice((nuint)offset, (nuint)count);
            span.Fill(value);
        }
#else
        public void PutSbyte(long offset, sbyte value)
        {
            AssertOffsetAndLength(offset, sizeof(sbyte));
            _buffer.Buffer[offset] = (byte)value;
        }

        public void PutByte(long offset, byte value)
        {
            AssertOffsetAndLength(offset, sizeof(byte));
            _buffer.Buffer[offset] = value;
        }

        public void PutByte(long offset, byte value, long count)
        {
            AssertOffsetAndLength(offset, sizeof(byte) * count);
            for (var i = 0; i < count; ++i)
                _buffer.Buffer[offset + i] = value;
        }
#endif

        // this method exists in order to conform with Java ByteBuffer standards
        public void Put(long offset, byte value)
          => PutByte(offset, value);

#if ENABLE_SPAN_T && UNSAFE_BYTEBUFFER
        public unsafe void PutStringUTF8(long offset, string value)
        {
            AssertOffsetAndLength(offset, value.Length);
            fixed (char* s = value)
            {
                fixed (byte* buffer = &_buffer.Span.GetReference())
                  Encoding.UTF8.GetBytes(s, value.Length, buffer + offset, (int)(LongLength - offset));
            }
        }
#elif ENABLE_SPAN_T && NETSTANDARD2_1
        public void PutStringUTF8(long offset, string value)
        {
            AssertOffsetAndLength(offset, value.Length);
            Encoding.UTF8.GetBytes(value.AsSpan().Slice(0, value.Length),
                _buffer.Span.Slice(offset));
        }
#else
        public void PutStringUTF8(long offset, string value)
        {
            AssertOffsetAndLength(offset, value.Length);
            Encoding.UTF8.GetBytes(value, 0, value.Length,
                _buffer.Buffer, offset);
        }
#endif

#if UNSAFE_BYTEBUFFER
        // Unsafe but more efficient versions of Put*.
        public void PutShort(long offset, short value)
          => PutUshort(offset, (ushort)value);

        public void PutUshort(long offset, ushort value)
        {
            AssertOffsetAndLength(offset, sizeof(ushort));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            var span = _buffer.Span.Slice((nuint)offset);
            BinaryPrimitives.WriteUInt16LittleEndian(span, value);
#else
            fixed (byte* ptr = _buffer.Buffer)
            {
                *(ushort*)(ptr + offset) = BitConverter.IsLittleEndian
                    ? value
                    : ReverseBytes(value);
            }
#endif
        }

        public void PutInt(long offset, int value)
          => PutUint(offset, (uint)value);

        public void PutUint(long offset, uint value)
        {
            AssertOffsetAndLength(offset, sizeof(uint));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            var span = _buffer.Span.Slice((nuint)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(span, value);
#else
            fixed (byte* ptr = _buffer.Buffer)
            {
                *(uint*)(ptr + offset) = BitConverter.IsLittleEndian
                    ? value
                    : ReverseBytes(value);
            }
#endif
        }

        public void PutLong(long offset, long value)
          => PutUlong(offset, (ulong)value);

        public void PutUlong(long offset, ulong value)
        {
            AssertOffsetAndLength(offset, sizeof(ulong));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            var span = _buffer.Span.Slice((nuint)offset);
            BinaryPrimitives.WriteUInt64LittleEndian(span, value);
#else
            fixed (byte* ptr = _buffer.Buffer)
            {
                *(ulong*)(ptr + offset) = BitConverter.IsLittleEndian
                    ? value
                    : ReverseBytes(value);
            }
#endif
        }

        public unsafe void PutFloat(long offset, float value)
        {
            AssertOffsetAndLength(offset, sizeof(float));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            fixed (byte* ptr = _buffer.Span)
#else
            fixed (byte* ptr = _buffer.Buffer)
#endif
            {
                if (BitConverter.IsLittleEndian)
                  *(float*)(ptr + offset) = value;
                else
                  *(uint*)(ptr + offset) = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(*(uint*)(&value));
            }
        }

        public unsafe void PutDouble(long offset, double value)
        {
            AssertOffsetAndLength(offset, sizeof(double));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            fixed (byte* ptr = _buffer.Span)
#else
            fixed (byte* ptr = _buffer.Buffer)
#endif
            {
                if (BitConverter.IsLittleEndian)
                  *(double*)(ptr + offset) = value;
                else
                  *(ulong*)(ptr + offset) = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(*(ulong*)(&value));
            }
        }
#else // !UNSAFE_BYTEBUFFER
        // Slower versions of Put* for when unsafe code is not allowed.
        public void PutShort(long offset, short value)
        {
            AssertOffsetAndLength(offset, sizeof(short));
            WriteLittleEndian(offset, sizeof(short), (ulong)value);
        }

        public void PutUshort(long offset, ushort value)
        {
            AssertOffsetAndLength(offset, sizeof(ushort));
            WriteLittleEndian(offset, sizeof(ushort), (ulong)value);
        }

        public void PutInt(long offset, int value)
        {
            AssertOffsetAndLength(offset, sizeof(int));
            WriteLittleEndian(offset, sizeof(int), (ulong)value);
        }

        public void PutUint(long offset, uint value)
        {
            AssertOffsetAndLength(offset, sizeof(uint));
            WriteLittleEndian(offset, sizeof(uint), (ulong)value);
        }

        public void PutLong(long offset, long value)
        {
            AssertOffsetAndLength(offset, sizeof(long));
            WriteLittleEndian(offset, sizeof(long), (ulong)value);
        }

        public void PutUlong(long offset, ulong value)
        {
            AssertOffsetAndLength(offset, sizeof(ulong));
            WriteLittleEndian(offset, sizeof(ulong), value);
        }

        public void PutFloat(long offset, float value)
        {
            AssertOffsetAndLength(offset, sizeof(float));
            // TODO(derekbailey): use BitConvert.SingleToInt32Bits() whenever flatbuffers upgrades to a .NET version
            // that contains it.
            ConversionUnion union;
            union.intValue = 0;
            union.floatValue = value;    
            WriteLittleEndian(offset, sizeof(float), (ulong)union.intValue);
        }

        public void PutDouble(long offset, double value)
        {
            AssertOffsetAndLength(offset, sizeof(double));
            WriteLittleEndian(offset, sizeof(double), (ulong)BitConverter.DoubleToInt64Bits(value));
        }

#endif // UNSAFE_BYTEBUFFER

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public sbyte GetSbyte(long index)
        {
            AssertOffsetAndLength(index, sizeof(sbyte));
            return (sbyte)_buffer.ReadOnlySpan[(nuint)index];
        }

        public byte Get(long index)
        {
            AssertOffsetAndLength(index, sizeof(byte));
            return _buffer.ReadOnlySpan[(nuint)index];
        }
#else
        public sbyte GetSbyte(long index)
        {
            AssertOffsetAndLength(index, sizeof(sbyte));
            return (sbyte)_buffer.Buffer[index];
        }

        public byte Get(long index)
        {
            AssertOffsetAndLength(index, sizeof(byte));
            return _buffer.Buffer[index];
        }
#endif

#if ENABLE_SPAN_T && UNSAFE_BYTEBUFFER
        public unsafe string GetStringUTF8(long startPos, long len)
        {
          if (len > int.MaxValue) throw new NotSupportedException("String longer than maximum signed 32-bit length.");

          fixed (byte* buffer = _buffer.ReadOnlySpan.Slice((nuint)startPos))
            return Encoding.UTF8.GetString(buffer, (int) len);
        }
#elif ENABLE_SPAN_T && NETSTANDARD2_1
        public string GetStringUTF8(int startPos, int len)
        {
            return Encoding.UTF8.GetString(_buffer.Span.Slice(startPos, len));
        }
#else
        public string GetStringUTF8(int startPos, int len)
        {
            return Encoding.UTF8.GetString(_buffer.Buffer, startPos, len);
        }
#endif

#if UNSAFE_BYTEBUFFER
        // Unsafe but more efficient versions of Get*.
        public short GetShort(long offset)
          => (short)GetUshort(offset);

        public ushort GetUshort(long offset)
        {
            AssertOffsetAndLength(offset, sizeof(ushort));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            var span = _buffer.ReadOnlySpan.Slice((nuint)offset, sizeof(ushort));
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(span);
#else
            fixed (byte* ptr = _buffer.Buffer)
            {
                return BitConverter.IsLittleEndian
                    ? *(ushort*)(ptr + offset)
                    : ReverseBytes(*(ushort*)(ptr + offset));
            }
#endif
        }

        public int GetInt(long offset)
          => (int)GetUint(offset);

        public uint GetUint(long offset)
        {
            AssertOffsetAndLength(offset, sizeof(uint));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            var span = _buffer.ReadOnlySpan.Slice((nuint)offset, sizeof(uint));
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(span);
#else
            fixed (byte* ptr = _buffer.Buffer)
            {
                return BitConverter.IsLittleEndian
                    ? *(uint*)(ptr + offset)
                    : ReverseBytes(*(uint*)(ptr + offset));
            }
#endif
        }

        public long GetLong(long offset)
          => (long)GetUlong(offset);

        public ulong GetUlong(long offset)
        {
            AssertOffsetAndLength(offset, sizeof(ulong));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            var span = _buffer.ReadOnlySpan.Slice((nuint)offset, sizeof(ulong));
            return System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(span);
#else            
            fixed (byte* ptr = _buffer.Buffer)
            {
                return BitConverter.IsLittleEndian
                    ? *(ulong*)(ptr + offset)
                    : ReverseBytes(*(ulong*)(ptr + offset));
            }
#endif
        }

        public unsafe float GetFloat(long offset)
        {
            AssertOffsetAndLength(offset, sizeof(float));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            fixed (byte* ptr = _buffer.ReadOnlySpan)
#else
            fixed (byte* ptr = _buffer.Buffer)
#endif
            {
              if (BitConverter.IsLittleEndian)
                return *(float*)(ptr + offset);
              var uValue = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(*(uint*)(ptr + offset));
              return *(float*)(&uValue);
            }
        }

        public unsafe double GetDouble(long offset)
        {
            AssertOffsetAndLength(offset, sizeof(double));
#if ENABLE_SPAN_T // && UNSAFE_BYTEBUFFER
            fixed (byte* ptr = _buffer.ReadOnlySpan)
#else
            fixed (byte* ptr = _buffer.Buffer)
#endif
            {
              if (BitConverter.IsLittleEndian)
                return *(double*)(ptr + offset);
              var uValue = System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(*(ulong*)(ptr + offset));
              return *(double*)(&uValue);
            }
        }
#else // !UNSAFE_BYTEBUFFER
        // Slower versions of Get* for when unsafe code is not allowed.
        public short GetShort(long index)
        {
            return (short)ReadLittleEndian(index, sizeof(short));
        }

        public ushort GetUshort(long index)
        {
            return (ushort)ReadLittleEndian(index, sizeof(ushort));
        }

        public int GetInt(long index)
        {
            return (int)ReadLittleEndian(index, sizeof(int));
        }

        public uint GetUint(long index)
        {
            return (uint)ReadLittleEndian(index, sizeof(uint));
        }

        public long GetLong(long index)
        {
            return (long)ReadLittleEndian(index, sizeof(long));
        }

        public ulong GetUlong(long index)
        {
            return ReadLittleEndian(index, sizeof(ulong));
        }

        public float GetFloat(long index)
        {
            // TODO(derekbailey): use BitConvert.Int32BitsToSingle() whenever flatbuffers upgrades to a .NET version
            // that contains it.
            ConversionUnion union;
            union.floatValue = 0;
            union.intValue = (int)ReadLittleEndian(index, sizeof(float));
            return union.floatValue;
        }

        public double GetDouble(long index)
        {
            return BitConverter.Int64BitsToDouble((long)ReadLittleEndian(index, sizeof(double)));
        }
#endif // UNSAFE_BYTEBUFFER

        /// <summary>
        /// Copies an array of type T into this buffer, ending at the given
        /// offset into this buffer. The starting offset is calculated based on the length
        /// of the array and is the value returned.
        /// </summary>
        /// <typeparam name="T">The type of the input data (must be a struct)</typeparam>
        /// <param name="offset">The offset into this buffer where the copy will end</param>
        /// <param name="x">The array to copy data from</param>
        /// <returns>The 'start' location of this buffer now, after the copy completed</returns>
        public long Put<T>(long offset, T[] x)
            where T : struct
        {
            if (x == null)
              throw new ArgumentNullException(nameof(x),"Cannot put a null array");

            if (x.LongLength == 0)
              throw new ArgumentException("Cannot put an empty array");

            if (!IsSupportedType<T>())
              throw new ArgumentException("Cannot put an array of type "
                + typeof(T) + " into this buffer");

            if (BitConverter.IsLittleEndian)
            {
                var numBytes = ArraySize(x);
                offset -= numBytes;
                AssertOffsetAndLength(offset, numBytes);
                // if we are LE, just do a block copy
#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
                MemoryMarshal.Cast<T, byte>(x).CopyTo(_buffer.Span.Slice((nuint)offset, (nuint)numBytes));
#else
                Buffer.BlockCopy(x, 0, _buffer.Buffer, offset, numBytes);
#endif
            }
            else
              throw new NotImplementedException("Big Endian Support not implemented yet for putting typed arrays");
            // if we are BE, we have to swap each element by itself
            //for(int i = x.Length - 1; i >= 0; i--)
            //{
            //  todo: low priority, but need to genericize the Put<T>() functions
            //}
            return offset;
        }

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        public long Put<T>(long offset, Span<T> x)
            where T : struct
        {
            if (x.Length == 0)
              throw new ArgumentException("Cannot put an empty array");

            if (!IsSupportedType<T>())
              throw new ArgumentException("Cannot put an array of type " + typeof(T) + " into this buffer");

            if (BitConverter.IsLittleEndian)
            {
                var numBytes = ArraySize(x);
                offset -= numBytes;
                AssertOffsetAndLength(offset, numBytes);
                // if we are LE, just do a block copy
                MemoryMarshal.Cast<T, byte>(x).CopyTo(_buffer.Span.Slice((nuint)offset, (nuint)numBytes));
            }
            else
              throw new NotImplementedException("Big Endian Support not implemented yet for putting typed arrays");
            // if we are BE, we have to swap each element by itself
            //for(int i = x.Length - 1; i >= 0; i--)
            //{
            //  todo: low priority, but need to genericize the Put<T>() functions
            //}
            return offset;
        }
#endif
    }
}
