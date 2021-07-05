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

using System;
using System.Text;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

#if NETSTANDARD
using nuint = System.UIntPtr;
using nint = System.IntPtr;
#endif

namespace BigBuffers
{
    /// <summary>
    /// All tables in the generated code derive from this struct, and add their own accessors.
    /// </summary>
    [PublicAPI]
    public struct Table
    {
        public int bb_pos { get; private set; }
        public ByteBuffer bb { get; private set; }

        public ByteBuffer ByteBuffer => bb;

        // Re-init the internal state with an external buffer {@code ByteBuffer} and an offset within.
        public Table(int _i, ByteBuffer _bb) : this()
        {
            bb = _bb;
            bb_pos = _i;
        }

        // Look up a field in the vtable, return an offset into the object, or 0 if the field is not
        // present.
        public long __offset(long vtableOffset)
        {
            var vtable = bb_pos - bb.GetLong(bb_pos);
            return vtableOffset < bb.GetShort(vtable) ? (long)bb.GetShort(vtable + vtableOffset) : 0;
        }

        public static long __offset(long vtableOffset, long offset, ByteBuffer bb)
        {
            var vtable = bb.LongLength - offset;
            return bb.GetShort(vtable + vtableOffset - bb.GetLong(vtable)) + vtable;
        }

        // Retrieve the relative offset stored at "offset"
        public long __indirect(long offset)
          => offset + bb.GetLong(offset);

        public static long __indirect(long offset, ByteBuffer bb)
          => offset + bb.GetLong(offset);

        // Create a .NET String from UTF-8 data stored inside the flatbuffer.
        public string __string(long offset)
        {
            offset += bb.GetLong(offset);
            var len = bb.GetLong(offset);
            var startPos = offset + sizeof(long);
            return bb.GetStringUTF8(startPos, len);
        }

        // Get the length of a vector whose offset is stored at "offset" in this object.
        public long __vector_len(long offset)
        {
            offset += bb_pos;
            offset += bb.GetLong(offset);
            return bb.GetLong(offset);
        }

        // Get the start of data of a vector whose offset is stored at "offset" in this object.
        public long __vector(long offset)
        {
            offset += bb_pos;
            return offset + bb.GetLong(offset) + sizeof(long);  // data starts after the length
        }

#if ENABLE_SPAN_T && (UNSAFE_BYTEBUFFER || NETSTANDARD2_1)
        // Get the data of a vector whoses offset is stored at "offset" in this object as an
        // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
        // then an empty span will be returned.
        public BigSpan<T> __vector_as_span<T>(long offset, long elementSize) where T : struct
        {
            if (!BitConverter.IsLittleEndian)
            {
               throw new NotSupportedException("Getting typed span on a Big Endian " +
                                               "system is not support");
            }

            var o = this.__offset(offset);
            if (0 == o)
            {
                return new BigSpan<T>();
            }

            var pos = this.__vector(o);
            var len = this.__vector_len(o);
            return bb.ToSpan(pos, len * elementSize).CastAs<T>();
        }
#else
        // Get the data of a vector whoses offset is stored at "offset" in this object as an
        // ArraySegment&lt;byte&gt;. If the vector is not present in the ByteBuffer,
        // then a null value will be returned.
        public ArraySegment<byte>? __vector_as_arraysegment(int offset)
        {
            var o = this.__offset(offset);
            if (0 == o)
            {
                return null;
            }

            var pos = this.__vector(o);
            var len = this.__vector_len(o);
            return bb.ToArraySegment(pos, len);
        }
#endif

        // Get the data of a vector whoses offset is stored at "offset" in this object as an
        // T[]. If the vector is not present in the ByteBuffer, then a null value will be
        // returned.
        public T[] __vector_as_array<T>(int offset)
            where T : struct
        {
            if(!BitConverter.IsLittleEndian)
            {
                throw new NotSupportedException("Getting typed arrays on a Big Endian " +
                    "system is not support");
            }

            var o = this.__offset(offset);
            if (0 == o)
            {
                return null;
            }

            var pos = this.__vector(o);
            var len = this.__vector_len(o);
            return bb.ToArray<T>(pos, len);
        }

        // Initialize any Table-derived type to point to the union at the given offset.
        public T __union<T>(int offset) where T : struct, IFlatbufferObject
        {
            var t = new T();
            t.__init(__indirect(offset), bb);
            return t;
        }

        public static bool __has_identifier(ByteBuffer bb, string ident)
        {
            if (ident.Length != Constants.FileIdentifierLength)
                throw new ArgumentException("BigBuffers: file identifier must be length " + Constants.FileIdentifierLength, "ident");

            for (var i = 0; i < Constants.FileIdentifierLength; i++)
            {
                if (ident[i] != (char)bb.Get((bb.Position + sizeof(int) + i))) return false;
            }

            return true;
        }

        // Compare strings in the ByteBuffer.
        public static int CompareStrings(long offset_1, long offset_2, ByteBuffer bb)
        {
            offset_1 += bb.GetLong(offset_1);
            offset_2 += bb.GetLong(offset_2);
            var len_1 = bb.GetLong(offset_1);
            var len_2 = bb.GetLong(offset_2);
            var startPos_1 = offset_1 + sizeof(long);
            var startPos_2 = offset_2 + sizeof(long);
            var len = Math.Min(len_1, len_2);
            for(var i = 0; i < len; i++) {
                var b1 = bb.Get(i + startPos_1);
                var b2 = bb.Get(i + startPos_2);
                if (b1 != b2)
                    return b1 - b2;
            }
            return Math.Sign(len_1 - len_2);
        }

        // Compare string from the ByteBuffer with the string object
        public static int CompareStrings(long offset_1, byte[] key, ByteBuffer bb)
        {
            offset_1 += bb.GetLong(offset_1);
            var len_1 = bb.GetLong(offset_1);
            var len_2 = key.Length;
            var startPos_1 = offset_1 + sizeof(long);
            var len = Math.Min(len_1, len_2);
            for (var i = 0; i < len; i++) {
                var b = bb.Get(i + startPos_1);
                if (b != key[i])
                    return b - key[i];
            }
            return Math.Sign(len_1 - len_2);
        }
    }
}
