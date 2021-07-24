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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

/// @file
/// @addtogroup flatbuffers_csharp_api
/// @{

namespace BigBuffers
{
  /// <summary>
  /// Responsible for building up and accessing a BigBuffer formatted byte
  /// array (via ByteBuffer).
  /// </summary>
  [PublicAPI]
  [DebuggerTypeProxy(typeof(BigBufferBuilderDebugger))]
  public class BigBufferBuilder
  {
    public ulong Space => _bb.LongLength - Offset;

    private ByteBuffer _bb;
    private ulong _minAlign = 1;

    public ulong Offset { get; set; }

    // The vtable for the current table (if _vtableSize > 0)
    private ulong[] _vtable = new ulong[16];
    // Whether or not a vtable has been started
    private bool _hasVtable;
    // The size of the vtable.
    private ulong _vtableSize;
    // Starting offset of the current struct/table.
    private ulong _tableStart;
    // List of offsets of all vtables.
    private ulong[] _vtables = new ulong[16];
    // Number of entries in `vtables` in use.
    private ulong _numVtables;
    // For the current vector being built.
    private Stack<(ulong start, ulong elemSize)> _vectorStarts = new();

    // For CreateSharedString
    private Dictionary<string, StringOffset> _sharedStringMap;

    /// <summary>
    /// Create a BigBufferBuilder with a given initial size.
    /// </summary>
    /// <param name="initialSize">
    /// The initial size to use for the internal buffer.
    /// Note: If this value is less than 8, it will assume the value is 8.
    /// </param>
    public BigBufferBuilder(ulong initialSize = 0)
      => _bb = new(Math.Max(8, initialSize));

    /// <summary>
    /// Create a BigBufferBuilder backed by the pased in ByteBuffer
    /// </summary>
    /// <param name="buffer">The ByteBuffer to write to</param>
    public BigBufferBuilder(ByteBuffer buffer)
    {
      _bb = buffer;
      buffer.Reset();
    }


    private void PushVectorStart(ulong start, ulong elemSize)
      => _vectorStarts.Push((start, elemSize));

    private ulong PopVectorStart(out ulong elemSize)
    {
      ulong start;
      (start, elemSize) = _vectorStarts.Pop();
      return start;
    }

    /// <summary>
    /// Reset the BigBufferBuilder by purging all data that it holds.
    /// </summary>
    public void Clear()
    {
      Offset = 0;
      _bb.Reset();
      _minAlign = 1;
      while (_vtableSize > 0) _vtable[--_vtableSize] = 0;
      _hasVtable = false;
      _vtableSize = 0;
      _tableStart = 0;
      _numVtables = 0;
      _vectorStarts = new();
      if (_sharedStringMap != null)
      {
        _sharedStringMap.Clear();
      }
    }

    /// <summary>
    /// Gets and sets a Boolean to disable the optimization when serializing
    /// default values to a Table.
    ///
    /// In order to save space, fields that are set to their default value
    /// don't get serialized into the buffer.
    /// </summary>
    public bool ForceDefaults { get; set; }

    /// @cond FLATBUFFERS_INTERNAL
    public void Pad(ulong size)
      => Offset += _bb.PutBytes(Offset, 0, size);

    private static readonly ulong[] growthPattern =
    {
      8,
      64,
      512,
      4096,
      65536,
      2097152,
      8388608,
      1073741824
    };

    void GrowBuffer(ulong needed = 0)
    {
      var l = Math.Max(_bb.LongLength + needed, (ulong)(_bb.LongLength * 1.5));
      var p = growthPattern[Array.FindLastIndex(growthPattern, x => x <= l) + 1];
      var n = (_bb.LongLength + p - 1) / p * p;
      _bb.GrowFront(n);
    }

    // Prepare to write an element of `size` after `additional_bytes`
    // have been written, e.g. if you write a string, you need to align
    // such the int length field is aligned to SIZEOF_INT, and the string
    // data follows it directly.
    // If all you need to do is align, `additional_bytes` will be 0.
    public void Prep(ulong size, ulong additionalBytes)
    {
      // Track the biggest thing we've ever aligned to.
      if (size > _minAlign)
        _minAlign = size;
      // Find the amount of alignment needed such that `size` is properly
      // aligned after `additional_bytes`
      var alignSize =
        ((~(_bb.LongLength - Space + additionalBytes)) + 1) &
        (size - 1);
      // Reallocate the buffer if needed.
      var needed = alignSize + size + additionalBytes;
      if (Space < needed)
        GrowBuffer(needed);
      if (alignSize > 0)
        Pad(alignSize);
    }

    public ulong Put<T>(T x)
      => Offset += _bb.Put<T>(Offset, x);

    public ulong Put<T>(in T x)
      => Offset += _bb.Put<T>(Offset, in x);

    /// <summary>
    /// Puts an array of type T into this builder at the
    /// current offset
    /// </summary>
    /// <typeparam name="T">The type of the input data </typeparam>
    /// <param name="x">The array to copy data from</param>
    public ulong Put<T>(T[] x)
      where T : unmanaged
      => Offset += _bb.Put<T>(Offset, x);

    /// <summary>
    /// Puts a span of type T into this builder at the
    /// current offset
    /// </summary>
    /// <typeparam name="T">The type of the input data </typeparam>
    /// <param name="x">The span to copy data from</param>
    public ulong Put<T>(ReadOnlySpan<T> x)
      where T : unmanaged
      => Offset += _bb.Put<T>(Offset, x);

    /// <summary>
    /// Puts a span of type T into this builder at the
    /// current offset
    /// </summary>
    /// <typeparam name="T">The type of the input data </typeparam>
    /// <param name="x">The span to copy data from</param>
    public ulong Put<T>(ReadOnlyBigSpan<T> x)
      => Offset += _bb.Put<T>(Offset, x);

    /// @endcond
    public ulong Add<T>(T x) where T : unmanaged
    {
      Prep(ByteBuffer.SizeOf<T>(), 0);
      return Put<T>(x);
    }

    public ulong Add<T>(T[] x)
      where T : unmanaged
    {
      if (x == null)
      {
        throw new ArgumentNullException(nameof(x), "Cannot add a null array");
      }

      if (x.Length == 0)
      {
        // don't do anything if the array is empty
        return 0;
      }

      if (!true)
      {
        throw new ArgumentException("Cannot add this Type array to the builder");
      }

      var size = (ulong)ByteBuffer.SizeOf<T>();
      // Need to prep on size (for data alignment) and then we pass the
      // rest of the length (minus 1) as additional bytes
      Prep(size, size * ((ulong)x.LongLength - 1));
      return Put(x);
    }

    /// <summary>
    /// Add a span of type T to the buffer (aligns the data and grows if necessary).
    /// </summary>
    /// <typeparam name="T">The type of the input data</typeparam>
    /// <param name="x">The span to copy data from</param>
    public ulong Add<T>(ReadOnlySpan<T> x)
      where T : unmanaged
    {
      if (!true)
      {
        throw new ArgumentException("Cannot add this Type array to the builder");
      }

      var size = (ulong)ByteBuffer.SizeOf<T>();
      // Need to prep on size (for data alignment) and then we pass the
      // rest of the length (minus 1) as additional bytes
      Prep(size, size * ((ulong)x.Length - 1));
      return Put(x);
    }

    /// <summary>
    /// Add a span of type T to the buffer (aligns the data and grows if necessary).
    /// </summary>
    /// <typeparam name="T">The type of the input data</typeparam>
    /// <param name="x">The span to copy data from</param>
    public ulong Add<T>(ReadOnlyBigSpan<T> x)
      where T : unmanaged
    {
      var size = (ulong)ByteBuffer.SizeOf<T>();
      // Need to prep on size (for data alignment) and then we pass the
      // rest of the length (minus 1) as additional bytes
      Prep(size, size * ((ulong)x.Length - 1));
      return Put(x);
    }


    /// <summary>
    /// Adds an offset, relative to where it will be written.
    /// </summary>
    /// <param name="off">The offset to add to the buffer.</param>
    public ulong AddOffset(ulong off)
    {
      Prep(sizeof(int), 0); // Ensure alignment is already done.
      if (off > Offset)
        throw new ArgumentException();

      off = Offset - off + sizeof(int);
      return Put(off);
    }

    /// @cond FLATBUFFERS_INTERNAL
    public void StartVector(ulong elemSize, ulong count)
    {
      NotNested();
      Prep(sizeof(long) + elemSize * count, 0);
      PushVectorStart(Offset, elemSize);
      Put(count);
    }
    /// @endcond
    /// <summary>
    /// Writes data necessary to finish a vector construction.
    /// </summary>
    public VectorOffset EndVector(uint alignment = 8)
    {
      var start = PopVectorStart(out var elemSize);
      var len = _bb.Get<ulong>(start);
      var expected = start + 8 + len * elemSize;
      if (Offset != expected)
        throw new InvalidOperationException($"Incomplete vector, {expected - Offset} bytes missing.");
      Prep(alignment, 0);
      return new(start);
    }

    /// <summary>
    /// Creates a vector of tables.
    /// </summary>
    /// <param name="offsets">Offsets of the tables.</param>
    public VectorOffset CreateVectorOfTables<T>(Offset<T>[] offsets)
    {
      NotNested();
      StartVector(sizeof(ulong), (ulong)offsets.LongLength);
      for (var i = offsets.LongLength - 1; i >= 0; i--) AddOffset(offsets[i].Value);
      return EndVector(sizeof(ulong));
    }

    /// @cond FLATBUFFERS_INTENRAL
    public void Nested(ulong obj)
    {
      // Structs are always stored inline, so need to be created right
      // where they are used. You'll get this assert if you created it
      // elsewhere.
      if (obj != Offset)
        throw new(
          "BigBuffers: unmanaged must be serialized inline.");
    }

    public void NotNested()
    {
      // You should not be creating any other objects or strings/vectors
      // while an object is being constructed
      if (_hasVtable)
        throw new(
          "BigBuffers: object serialization must not be nested.");
    }

    public void StartTable(uint numFields)
    {
      NotNested();

      if (_vtable.Length < numFields)
        _vtable = new ulong[numFields];

      _hasVtable = true;
      _vtableSize = numFields;

      // Write placeholder for vtable offset
      Add(ulong.MaxValue);
      _tableStart = Offset - 8;
    }


    // Set the current vtable at `voffset` to the current location in the
    // buffer.
    public void Slot(ulong vOffset, ulong size = 0)
    {
      if (vOffset >= _vtableSize)
        throw new ArgumentOutOfRangeException(nameof(vOffset));

      _vtable[vOffset] = Offset - size;
    }

    /// <summary>
    /// Adds a <typeparamref name="T"/> to the Table at index <paramref name="o"/>
    /// in its vtable using the value <paramref name="value"/> and default <paramref name="default"/>.
    /// </summary>
    /// <param name="o">The index into the vtable</param>
    /// <param name="value">The value to put into the buffer. If the value is equal to the default
    /// and <see cref="ForceDefaults"/> is false, the value will be skipped.</param>
    /// <param name="default">The default value to compare the value against</param>
    public void Add<T>(ulong o, in T value, in T @default) where T : unmanaged
    {
      if (!ForceDefaults)
      {
        var valueSpan = ReadOnlyBigSpan.Create(value, 1);
        var defaultSpan = ReadOnlyBigSpan.Create(@default, 1);
        if (valueSpan.SequenceEqual(defaultSpan))
          return;
      }
      Slot(o);
      Add(value);
    }


    /// <summary>
    /// Adds a <typeparamref name="T"/> to the Table at index <paramref name="o"/>
    /// in its vtable using the nullable value <paramref name="x"/>.
    /// </summary>
    /// <param name="o">The index into the vtable</param>
    /// <param name="x">The nullable boolean value to put into the buffer. If it doesn't have a value
    /// it will skip writing to the buffer.</param>
    public void Add<T>(ulong o, T? x) where T : unmanaged
    {
      if (ForceDefaults)
      {
        Slot(o);
        Add(x ?? default(T));
      }
      else
      {
        if (!x.HasValue) return;
        Slot(o);
        Add(x.Value);
      }
    }

    /// <summary>
    /// Adds a buffer offset to the Table at index `o` in its vtable using the value `x` and default `d`
    /// </summary>
    /// <param name="o">The index into the vtable</param>
    /// <param name="x">The value to put into the buffer. If the value is equal to the default
    /// the value will be skipped.</param>
    /// <param name="d">The default value to compare the value against</param>
    public void AddOffset(ulong o, ulong x, ulong d)
      => Add(o, x, d);
    /// @endcond
    /// <summary>
    /// Encode the string `s` in the buffer using UTF-8.
    /// </summary>
    /// <param name="s">The string to encode.</param>
    /// <returns>
    /// The offset in the buffer where the encoded string starts.
    /// </returns>
    public StringOffset CreateString(string s)
    {
      if (s == null)
      {
        return new(0);
      }
      NotNested();
      var strLen = (ulong)Encoding.UTF8.GetByteCount(s);
      StartVector(1, strLen + 1);
      Offset += _bb.PutStringUtf8(Offset, strLen, s);
      Add<byte>(0);
      return new(EndVector(1).Value);
    }

    public StringOffset CreateString(out Placeholder placeholder)
    {
      placeholder = new(this, Offset);
      return new(ulong.MaxValue);
    }
    public VectorOffset CreateVector(out Placeholder placeholder)
    {
      placeholder = new(this, Offset);
      return new(ulong.MaxValue);
    }
    public Offset<T> CreateOffset<T>(out Placeholder<T> placeholder)
    {
      placeholder = new(this, Offset);
      return new(ulong.MaxValue);
    }

    /// <summary>
    /// Creates a string in the buffer from a Span containing
    /// a UTF8 string.
    /// </summary>
    /// <param name="chars">the UTF8 string to add to the buffer</param>
    /// <returns>
    /// The offset in the buffer where the encoded string starts.
    /// </returns>
    public StringOffset CreateUtf8String(BigSpan<byte> chars)
    {
      NotNested();
      Add<byte>(0);
      var utf8StringLen = (ulong)chars.Length;
      StartVector(1, utf8StringLen);
      Offset += _bb.Put<byte>(Offset, chars);
      return new(EndVector(1).Value);
    }

    /// <summary>
    /// Store a string in the buffer, which can contain any binary data.
    /// If a string with this exact contents has already been serialized before,
    /// instead simply returns the offset of the existing string.
    /// </summary>
    /// <param name="s">The string to encode.</param>
    /// <returns>
    /// The offset in the buffer where the encoded string starts.
    /// </returns>
    public StringOffset CreateSharedString(string s)
    {
      if (s == null)
      {
        return new(0);
      }

      if (_sharedStringMap == null)
      {
        _sharedStringMap = new();
      }

      if (_sharedStringMap.ContainsKey(s))
      {
        return _sharedStringMap[s];
      }

      var stringOffset = CreateString(s);
      _sharedStringMap.Add(s, stringOffset);
      return stringOffset;
    }

    /// @cond FLATBUFFERS_INTERNAL
    // Structs are stored inline, so nothing additional is being added.
    // `d` is always 0.
    public void AddStruct(ulong size, ulong voffset, ulong x, ulong d)
    {
      if (x == d) return;

      Nested(x + size);
      Slot(voffset, size);
    }

    public ulong EndTable()
    {
      if (!_hasVtable)
        throw new InvalidOperationException(
          "Flatbuffers: calling EndTable without a StartTable");

      var paddingBytes = sizeof(ulong) - (Offset & (sizeof(ulong) - 1));
      if (paddingBytes < 8)
        Pad(paddingBytes);

      var vtableStart = Offset;

      var trimmedSize = _vtableSize;

      // Trim trailing empty fields.
      while (trimmedSize > 0 && _vtable[trimmedSize - 1] == 0)
        trimmedSize--;

      var vtableSize = (ushort)((trimmedSize + 2) * sizeof(ushort));
      Add<ushort>(vtableSize);

      var tableSize = (ushort)(vtableStart - _tableStart);
      Add<ushort>(tableSize);

      if (trimmedSize > 0)
      {

        for (var i = 0uL; i < trimmedSize; i++)
        {
          // Offset relative to the start of the table.
          var o = i;
          var shortOffset = checked(
            (ushort)(
              _vtable[o] != 0
                ? _vtable[o] - _tableStart
                : 0
            )
          );
          Add<ushort>(shortOffset);

          // clear out written entry
          _vtable[i] = 0;
        }
      }

      // Search for an existing vtable that matches the current one.
      ulong existingVtable = 0;
      for (var i = 0uL; i < _numVtables; i++)
      {
        var vt1 = _bb.LongLength - _vtables[i];
        var vt2 = Space;
        var len = (ulong)_bb.Get<ushort>(vt1);
        if (len == _bb.Get<ushort>(vt2))
        {
          for (ulong j = sizeof(short); j < len; j += sizeof(short))
          {
            if (_bb.Get<ushort>(vt1 + j) != _bb.Get<ushort>(vt2 + j))
              goto endLoop;
          }
          existingVtable = _vtables[i];
          break;
        }

        endLoop:
        { }
      }

      if (existingVtable != 0)
      {
        //throw new NotImplementedException();
        // Found a match:
        // Remove the current vtable.
        Offset = vtableStart;
        // Point table to existing vtable.
        _bb.Put(_tableStart, _tableStart - existingVtable);
      }
      else
      {
        // No match:
        // Add the location of the current vtable to the list of
        // vtables.
        if (_numVtables == (ulong)_vtables.LongLength)
        {
          // Arrays.CopyOf(vtables num_vtables * 2);
          var newVTables = new ulong[_numVtables * 2];
          Array.Copy(_vtables, newVTables, _vtables.Length);

          _vtables = newVTables;
        }
        _vtables[_numVtables++] = vtableStart;
        // Point table to current vtable.
        _bb.Put<ulong>(_tableStart, _tableStart - vtableStart);
      }

      _hasVtable = false;
      _vtableSize = 0;
      return _tableStart;
    }

    // This checks a required field has been set in a given table that has
    // just been constructed.
    public void Required(ulong table, ulong field)
    {
      var vtable = table - _bb.Get<ulong>(table);
      var ok = _bb.Get<ushort>(vtable + field) != 0;
      // If this fails, the caller will show what field needs to be set.
      if (!ok)
        throw new InvalidOperationException("BigBuffers: field " + field +
          " must be set");
    }
    /// @endcond
    /// <summary>
    /// Finalize a buffer, pointing to the given `root_table`.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    /// <param name="sizePrefix">
    /// Whether to prefix the size to the buffer.
    /// </param>
    protected void Finish(ulong rootTable, bool sizePrefix)
    {
      Prep(_minAlign, sizeof(ulong) + (sizePrefix ? sizeof(ulong) : 0uL));
      AddOffset(rootTable);
      if (sizePrefix)
      {
        Add<ulong>(_bb.LongLength - Space);
      }
      _bb.Position = Offset;
    }

    /// <summary>
    /// Finalize a buffer, pointing to the given `root_table`.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    public void Finish(ulong rootTable)
      => Finish(rootTable, false);

    /// <summary>
    /// Finalize a buffer, pointing to the given `root_table`, with the size prefixed.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    public void FinishSizePrefixed(ulong rootTable)
      => Finish(rootTable, true);

    /// <summary>
    /// Get the ByteBuffer representing the BigBuffer.
    /// </summary>
    /// <remarks>
    /// This is typically only called after you call `Finish()`.
    /// The actual data starts at the ByteBuffer's current position,
    /// not necessarily at `0`.
    /// </remarks>
    /// <returns>
    /// Returns the ByteBuffer for this BigBuffer.
    /// </returns>
    public ByteBuffer ByteBuffer => _bb;

    /// <summary>
    /// A utility function to copy and return the ByteBuffer data as a
    /// `byte[]`.
    /// </summary>
    /// <returns>
    /// A full copy of the BigBuffer data.
    /// </returns>
    public byte[] SizedByteArray()
      => _bb.ToArray(0, Offset);

    /// <summary>
    /// A utility function to copy and return the ByteBuffer data as a
    /// `byte[]`.
    /// </summary>
    /// <returns>
    /// A full copy of the BigBuffer data.
    /// </returns>
    public ArraySegment<byte> SizedByteArraySegment()
      => _bb.ToArraySegment(0, checked((int)Offset));

    /// <summary>
    /// A utility function to copy and return the ByteBuffer data as a
    /// <see cref="BigSpan{Byte}"/>.
    /// </summary>
    /// <returns>
    /// A full copy of the BigBuffer data.
    /// </returns>
    public BigSpan<byte> SizedSpan()
      => _bb.ToSpan(0, Offset);

    /// <summary>
    /// A utility function to copy and return the ByteBuffer data as a
    /// <see cref="ReadOnlyBigSpan{Byte}"/>.
    /// </summary>
    /// <returns>
    /// A full copy of the BigBuffer data.
    /// </returns>
    public ReadOnlyBigSpan<byte> SizedReadOnlySpan()
      => _bb.ToReadOnlySpan(0, Offset);

    /// <summary>
    /// Finalize a buffer, pointing to the given `rootTable`.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    /// <param name="fileIdentifier">
    /// A BigBuffer file identifier to be added to the buffer before
    /// `root_table`.
    /// </param>
    /// <param name="sizePrefix">
    /// Whether to prefix the size to the buffer.
    /// </param>
    protected void Finish(ulong rootTable, string fileIdentifier, bool sizePrefix)
    {
      Prep(_minAlign, sizeof(ulong) + (sizePrefix ? sizeof(ulong) : 0uL) +
        Constants.FileIdentifierLength);
      if (fileIdentifier.Length !=
        Constants.FileIdentifierLength)
        throw new ArgumentException(
          "BigBuffers: file identifier must be length " +
          Constants.FileIdentifierLength,
          nameof(fileIdentifier));
      var i = Constants.FileIdentifierLength - 1;
      var l = fileIdentifier.Length;
      for (; i > l; i--)
        Add<byte>(0);
      for (; i >= 0; i--)
        Add<byte>((byte)fileIdentifier[i]);
      Finish(rootTable, sizePrefix);
    }

    /// <summary>
    /// Finalize a buffer, pointing to the given `rootTable`.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    /// <param name="fileIdentifier">
    /// A BigBuffer file identifier to be added to the buffer before
    /// `root_table`.
    /// </param>
    public void Finish(ulong rootTable, string fileIdentifier)
      => Finish(rootTable, fileIdentifier, false);

    /// <summary>
    /// Finalize a buffer, pointing to the given `rootTable`, with the size prefixed.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    /// <param name="fileIdentifier">
    /// A BigBuffer file identifier to be added to the buffer before
    /// `root_table`.
    /// </param>
    public void FinishSizePrefixed(ulong rootTable, string fileIdentifier)
      => Finish(rootTable, fileIdentifier, true);
  }
}

/// @}
