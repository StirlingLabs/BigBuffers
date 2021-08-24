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
//      BYTEBUFFER_NO_BOUNDS_CHECK
//          This will disable the bounds check asserts to the byte array. This can
//          yield a small performance gain in normal code.
//
// Using UNSAFE_BYTEBUFFER and BYTEBUFFER_NO_BOUNDS_CHECK together can yield a
// performance gain of ~15% for some operations, however doing so is potentially
// dangerous. Do so at your own risk!
//

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Win32.SafeHandles;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Magic;

namespace BigBuffers
{
  [DebuggerTypeProxy(typeof(ByteBufferDebugger))]
  public struct ByteBuffer : IEquatable<ByteBuffer>
  {
    internal readonly ByteBufferManager Buffer;

    private ulong _pos; // Must track start of the buffer.

    public ByteBuffer(ByteBufferManager manager, ulong position)
    {
      Buffer = manager;
      _pos = position;
    }

    public ByteBuffer(ulong size) : this(new byte[size]) { }

    public ByteBuffer(byte[] buffer) : this(buffer, 0) { }

    public ByteBuffer(SafeBuffer buffer) : this(buffer, 0) { }

    /// <summary>
    /// WARNING: This implementation expects you to have
    /// pinned the span for the duration of use.
    /// </summary>
    public ByteBuffer(BigSpan<byte> buffer) : this(buffer, 0) { }

    public ByteBuffer(byte[] buffer, ulong pos)
    {
      Buffer = new ByteArrayManager(buffer);
      _pos = pos;
    }
    public ByteBuffer(SafeBuffer buffer, ulong pos, bool growable = false)
    {
      Buffer = new SafeBufferManager(buffer, growable);
      _pos = pos;
    }
    /// <summary>
    /// WARNING: This implementation expects you to have
    /// pinned the span for the duration of use.
    /// </summary>
    public ByteBuffer(BigSpan<byte> buffer, ulong pos, bool growable = false)
    {
      Buffer = new UnsafeByteSpanManager(buffer, growable);
      _pos = pos;
    }

    public ulong Position
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => _pos;
      set => _pos = value;
    }

    public uint Length => Buffer.Length;

    public ulong LongLength => Buffer.LongLength;

    public void Reset()
      => _pos = 0;

    // Create a new ByteBuffer on the same underlying data.
    // The new ByteBuffer's position will be same as this buffer's.
    public ByteBuffer Duplicate()
      => new(Buffer, Position);

    // Increases the size of the ByteBuffer, and copies the old data towards
    // the end of the new buffer.
    public void Resize(ulong newSize)
      => Buffer.GrowFront(newSize);

    public byte[] ToArray(ulong pos, ulong len)
      => ToArray<byte>(pos, len);

    /// <summary>
    /// Get the wire-size (in bytes) of a type supported by flatbuffers.
    /// </summary>
    /// <typeparam name="T">The type to get the wire size of</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong SizeOf<T>()
      => (ulong)Unsafe.SizeOf<T>();

    /// <summary>
    /// Get the wire-size (in bytes) of an typed array
    /// </summary>
    /// <typeparam name="T">The type of the array</typeparam>
    /// <param name="count">The element count to get the size of</param>
    /// <returns>The number of bytes the array takes on wire</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ArraySize<T>(ulong count)
      => SizeOf<T>() * count;

    /// <summary>
    /// Get the wire-size (in bytes) of an typed array
    /// </summary>
    /// <typeparam name="T">The type of the array</typeparam>
    /// <param name="x">The array to get the size of</param>
    /// <returns>The number of bytes the array takes on wire</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ArraySize<T>(T[] x) where T : unmanaged
      => ArraySize<T>((ulong)x.LongLength);

    /// <summary>
    /// Get the wire-size (in bytes) of an typed array
    /// </summary>
    /// <typeparam name="T">The type of the array</typeparam>
    /// <param name="x">The array to get the size of</param>
    /// <returns>The number of bytes the array takes on wire</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ArraySize<T>(ReadOnlySpan<T> x) where T : unmanaged
      => ArraySize<T>((ulong)x.Length);

    /// <summary>
    /// Get the wire-size (in bytes) of an typed array
    /// </summary>
    /// <typeparam name="T">The type of the array</typeparam>
    /// <param name="x">The array to get the size of</param>
    /// <returns>The number of bytes the array takes on wire</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ArraySize<T>(ReadOnlyBigSpan<T> x)
      => ArraySize<T>(x.Length);

    // Get a portion of the buffer casted into an array of type T, given
    // the buffer position and length.
    public readonly T[] ToArray<T>(ulong pos, ulong len)
      where T : unmanaged
    {
      AssertOffsetAndLength(pos, len);

      var array = new T[len];

      var span = Buffer.ReadOnlySpan.Slice((nuint)pos, (nuint)ArraySize(array)).CastAs<T>();

      span.CopyTo(new(array));

      return array;
    }

    public byte[] ToSizedArray()
      => ToArray<byte>(Position, LongLength - Position);

    public byte[] ToFullArray()
      => ToArray<byte>(0, LongLength);

    public readonly BigSpan<byte> ToSpan(ulong pos, ulong len)
      => Buffer.Span.Slice((nuint)pos, (nuint)len);
    public ReadOnlyBigSpan<byte> ToReadOnlySpan(ulong pos, ulong len)
      => Buffer.ReadOnlySpan.Slice((nuint)pos, (nuint)len);
    // Helper functions for the unsafe version.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly void AssertOffsetAndLength(ulong offset, ulong length)
    {
#if !BYTEBUFFER_NO_BOUNDS_CHECK
      if (offset + length > Buffer.LongLength)
        throw new ArgumentOutOfRangeException(nameof(offset));
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong PutBytes(ulong offset, byte value, ulong count)
    {
      // slice will throw if out of range
      //AssertOffsetAndLength(offset, count);
      var span = Buffer.Span.Slice((nuint)offset, (nuint)count);
      span.Fill(value);
      return count;
    }

    // this method exists in order to conform with Java ByteBuffer standards
    //public void Put(long offset, byte value)
    //  => PutByte(offset, value);

#if NETSTANDARD2_1 || NET5_0_OR_GREATER
    public ulong PutStringUtf8(ulong offset, ulong byteLength, string value)
    {
      var span = (Span<byte>)Buffer.Span.Slice((nuint)offset, (nuint)byteLength);
      var bytesLength = Encoding.UTF8.GetBytes(value.AsSpan()[..value.Length], span);
      return (ulong)bytesLength;
    }
#else
    public unsafe ulong PutStringUtf8(ulong offset, ulong byteLength, string value)
    {
      fixed (char* s = value)
      fixed (byte* buffer = Buffer.Span)
      {
        Encoding.UTF8.GetBytes(s, value.Length,
          buffer + offset, checked((int)byteLength));
        return byteLength;
      }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly T Get<T>(ulong index) where T : unmanaged
    {
      var itemSize = SizeOf<T>();
      AssertOffsetAndLength(index, itemSize);
      ref var item = ref Buffer.Span[(nuint)index];
      if (BitConverter.IsLittleEndian)
        return Unsafe.As<byte, T>(ref item);
      switch (itemSize)
      {
        case 1: {
          return Unsafe.As<byte, T>(ref item);
        }
        case 2: {
          var v = BinaryPrimitives.ReverseEndianness((ushort)item);
          return Unsafe.As<ushort, T>(ref v);
        }
        case 4: {
          var v = BinaryPrimitives.ReverseEndianness((uint)item);
          return Unsafe.As<uint, T>(ref v);
        }
        case 8: {
          var v = BinaryPrimitives.ReverseEndianness((ulong)item);
          return Unsafe.As<ulong, T>(ref v);
        }
        default:
          throw new NotImplementedException($"{typeof(T).FullName}");
      }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Ref<T>(ulong index) where T : unmanaged
    {
      var itemSize = SizeOf<T>();
      AssertOffsetAndLength(index, itemSize);
      ref var item = ref Buffer.Span[(nuint)index];
      if (!BitConverter.IsLittleEndian)
        throw new NotImplementedException($"Ref<{typeof(T).FullName}>");

      return ref Unsafe.As<byte, T>(ref item);
    }

    // NOTE: no explicit bounds check on these; they will throw IndexOutOfRangeException if there is any problem

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly byte GetByte(ulong index)
      => Buffer.Span[(nuint)index];

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref byte RefByte(ulong index)
      => ref Buffer.Span[(nuint)index];

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public BigSpan<T> GetSpan<T>(ulong index, ulong size)
      => BigSpan.Create(ref Unsafe.As<byte, T>(ref Buffer.Span[(nuint)index]), (nuint)size);

    public static readonly ConditionalWeakTable<ByteBufferManager, ConcurrentDictionary<(ulong startPos, int len), WeakReference<string>>>
      PerByteBufferStringCache =
        new();

    public readonly ConcurrentDictionary<(ulong startPos, int len), WeakReference<string>> StringCache
      => PerByteBufferStringCache.GetValue(Buffer, _ => new());

    [MethodImpl(MethodImplOptions.NoInlining)]
    public readonly string GetStringUtf8(ulong startPos, int len)
    {
      var self = this;

#if NETSTANDARD2_1 || NET5_0_OR_GREATER
      string StringFactory((ulong startPos, int len) t)
        => Encoding.UTF8.GetString(self.Buffer.Span.Slice((nuint)t.startPos, t.len));

#else
      unsafe string StringFactory((ulong startPos, int len) t)
      {
        fixed (byte* buffer = self.Buffer.ReadOnlySpan.Slice((nuint)t.startPos))
          return Encoding.UTF8.GetString(buffer, t.len);
      }
#endif

      WeakReference<string> StringWeakReferenceFactory((ulong startPos, int len) t)
        => new(StringFactory(t));

      var k = (startPos, len);
      var wr = StringCache.GetOrAdd(k, StringWeakReferenceFactory);
      if (wr.TryGetTarget(out var s))
        return s;

      s = StringFactory(k);
      wr.SetTarget(s);
      return s;
    }

    /// <summary>
    /// Copies an array of type T into this buffer, ending at the given
    /// offset into this buffer. The starting offset is calculated based on the length
    /// of the array and is the value returned.
    /// </summary>
    /// <typeparam name="T">The type of the input data (must be a struct)</typeparam>
    /// <param name="offset">The offset into this buffer where the copy will end</param>
    /// <param name="x">The array to copy data from</param>
    /// <returns>The 'start' location of this buffer now, after the copy completed</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Put<T>(ulong offset, T[] x)
      where T : unmanaged
    {
      if (x is null)
        throw new ArgumentNullException(nameof(x), "Cannot put a null array");

      if (x.LongLength == 0)
        throw new ArgumentException("Cannot put an empty array");

      var numBytes = ArraySize(x);
      //var start = checked(offset - numBytes);
      // slice will throw if out of range
      //AssertOffsetAndLength(start, numBytes);
      if (BitConverter.IsLittleEndian)
      {
        // if we are LE, just do a block copy
        new ReadOnlyBigSpan<T>(x).AsBytes()
          .CopyTo(Buffer.Span.Slice((nuint)offset, (nuint)numBytes));
      }
      else
        throw new NotImplementedException("Big Endian Support not implemented yet for putting typed arrays");
      return numBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Put<T>(ulong offset, ReadOnlyBigSpan<T> x)
    {
      if (x.Length == 0)
        throw new ArgumentException("Cannot put an empty array");

      var numBytes = ArraySize(x);
      //var start = checked(offset - numBytes);
      // slice will throw if out of range
      //AssertOffsetAndLength(start, numBytes);
      if (BitConverter.IsLittleEndian)
      {
        x.AsBytes()
          .CopyTo(Buffer.Span.Slice((nuint)offset, (nuint)numBytes));
      }
      else
        throw new NotImplementedException("Big Endian Support not implemented yet for putting typed arrays");
      return numBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Put<T>(ulong offset, T x)
    {
      var numBytes = SizeOf<T>();
      var longNumBytes = (int)numBytes;
      AssertOffsetAndLength(offset, numBytes);
      ref var target = ref Buffer.Span[(nuint)offset];

      if (BitConverter.IsLittleEndian)
        Unsafe.As<byte, T>(ref target) = x;
      else
      {
        ref var xRef = ref Unsafe.AsRef(x);
        switch (longNumBytes)
        {
          case 1:
            Unsafe.As<byte, T>(ref target) = x;
            break;
          case 2:
            Unsafe.As<byte, ushort>(ref target) =
              BinaryPrimitives.ReverseEndianness
                (Unsafe.As<T, ushort>(ref xRef));
            break;
          case 4:
            Unsafe.As<byte, uint>(ref target) =
              BinaryPrimitives.ReverseEndianness
                (Unsafe.As<T, uint>(ref xRef));
            break;
          case 8:
            Unsafe.As<byte, ulong>(ref target) =
              BinaryPrimitives.ReverseEndianness
                (Unsafe.As<T, ulong>(ref xRef));
            break;
          default:
            throw new NotImplementedException($"Big Endian Support not implemented yet for {typeof(T).FullName}");
        }
      }
      return numBytes;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong Put<T>(ulong offset, in T x)
    {
      var numBytes = SizeOf<T>();
      var longNumBytes = (int)numBytes;
      AssertOffsetAndLength(offset, numBytes);
      ref var target = ref Buffer.Span[(nuint)offset];

      if (BitConverter.IsLittleEndian)
        Unsafe.As<byte, T>(ref target) = x;
      else
      {
        ref var xRef = ref Unsafe.AsRef(x);
        switch (longNumBytes)
        {
          case 1:
            Unsafe.As<byte, T>(ref target) = x;
            break;
          case 2:
            Unsafe.As<byte, ushort>(ref target) =
              BinaryPrimitives.ReverseEndianness
                (Unsafe.As<T, ushort>(ref xRef));
            break;
          case 4:
            Unsafe.As<byte, uint>(ref target) =
              BinaryPrimitives.ReverseEndianness
                (Unsafe.As<T, uint>(ref xRef));
            break;
          case 8:
            Unsafe.As<byte, ulong>(ref target) =
              BinaryPrimitives.ReverseEndianness
                (Unsafe.As<T, ulong>(ref xRef));
            break;
          default:
            throw new NotImplementedException($"Big Endian Support not implemented yet for {typeof(T).FullName}");
        }
      }
      return numBytes;
    }
    public bool Equals(ByteBuffer other)
      => Equals(Buffer, other.Buffer) && _pos == other._pos;

    public override bool Equals(object obj)
      => obj is ByteBuffer other && Equals(other);

    public override int GetHashCode()
    {
      unchecked
      {
        // ReSharper disable once NonReadonlyMemberInGetHashCode
        return ((Buffer is not null ? Buffer.GetHashCode() : 0) * 397) ^ _pos.GetHashCode();
      }
    }
    public static bool operator ==(ByteBuffer left, ByteBuffer right)
      => left.Equals(right);

    public static bool operator !=(ByteBuffer left, ByteBuffer right)
      => !left.Equals(right);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ref ByteBuffer UnsafeSelfReference()
      => ref Unsafe.AsRef<ByteBuffer>(Unsafe.AsPointer(ref Unsafe.AsRef(this)));

    public static ulong AlignOf<T>()
      => IfType<T>.IsPrimitive()
        ? SizeOf<T>()
        : IfType<T>.IsAssignableTo<IBigBufferStruct>()
          ? TypeAlignments.GetOrAdd(typeof(T), GetAlignment)
          : sizeof(ulong);

    private static ulong GetAlignment(Type t)
      => (ulong)Info.OfMethod<ByteBuffer>("GetAlignment").MakeGenericMethod(t).Invoke(null, null)!;

    private static readonly ConcurrentDictionary<Type, ulong> TypeAlignments = new();

    private ulong GetAlignment<T>()
      where T : struct, IBigBufferStruct
      => Unsafe.NullRef<T>().Alignment;
  }
}
