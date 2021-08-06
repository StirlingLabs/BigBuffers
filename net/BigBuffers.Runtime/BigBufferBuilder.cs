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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using StirlingLabs.Utilities;

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
    public static bool UseExistingVTables = true;

    internal const ulong PlaceholderOffset = unchecked((ulong)long.MinValue);

    public ulong Space => _bb.LongLength - Offset;

    private ByteBuffer _bb;
    private ulong _minAlign = 1;

    public ulong Offset { get; set; }

    // The vtable for the current table
    private ulong[] _vtable = new ulong[16];
    // The size of the vtable.
    private ulong _vtableUsed;
    // Starting offset of the current struct/table.
    private ulong _tableStart;
    // List of offsets of all vtables, excluding the current one.
    private List<ulong> _writtenVTables = new();
    // For the vectors being built.
    private Stack<(ulong start, ulong elemSize)> _vectorStarts = new();

    // For CreateSharedString
    private ConcurrentDictionary<string, StringOffset> _sharedStringMap = new();

    /// <summary>
    /// Create a BigBufferBuilder with a given initial size.
    /// </summary>
    /// <param name="initialSize">
    /// The initial size to use for the internal buffer.
    /// Note: If this value is less than 32, it will assume the value is 32.
    /// </param>
    public BigBufferBuilder(ulong initialSize = 0)
      => _bb = new(Math.Max(sizeof(ulong) * 4, initialSize));

    /// <summary>
    /// Create a BigBufferBuilder backed by the <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The ByteBuffer to write to</param>
    public BigBufferBuilder(ByteBuffer buffer)
    {
      _bb = buffer;
      buffer.Reset();
    }


    internal void PushVectorStart(ulong start, ulong elemSize)
      => _vectorStarts.Push((start, elemSize));

    internal ulong PopVectorStart(out ulong elemSize)
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
      while (_vtableUsed > 0) _vtable[--_vtableUsed] = 0;
      _vtableUsed = 0;
      _tableStart = 0;
      _writtenVTables = new();
      _vectorStarts = new();
      if (_sharedStringMap is not null)
        _sharedStringMap.Clear();
    }

    /// <summary>
    /// Gets and sets a Boolean to disable the optimization when serializing
    /// default values to a Table.
    ///
    /// In order to save space, fields that are set to their default value
    /// don't get serialized into the buffer.
    /// </summary>
    public bool ForceDefaults { get; set; }

    public void Pad(ulong size)
      => Offset += _bb.PutBytes(Offset, 0, size);

    private static readonly ulong[] GrowthPattern =
    {
      1,
      32,
      128,
      512,
      4096,
      65536,
      2097152,
      8388608,
      67108864
    };

    private bool _tableStarted;
    private ulong _sizePrefixOffset;

    void GrowBuffer(ulong needed)
    {
      if (needed == 0)
        throw new ArgumentOutOfRangeException(nameof(needed), "Must need more than 0 bytes.");

      var currentBufferSize = _bb.LongLength;

      var minimum = currentBufferSize + needed;

      var halfMin = minimum >> 1;

      var patternIndex = Array.FindLastIndex(GrowthPattern, patternValue => patternValue <= halfMin)
        + 1;

      var multiple = GrowthPattern[patternIndex];

      var size = (minimum + multiple - 1) / multiple * multiple;

      _bb.Resize(size);
    }

    // Prepare to write an element of `align` after `size`
    // have been written, e.g. if you write a string, you need to align
    // such the int length field is aligned to SIZEOF_INT, and the string
    // data follows it directly.
    // If all you need to do is align, `size` will be 0.
    public void Prep(ulong align, ulong size)
    {
      Debug.Assert(align != 0);
      // Track the biggest thing we've ever aligned to.
      if (align > _minAlign)
        _minAlign = align;
      // Find the amount of alignment needed such that `size` is properly
      // aligned after `additional_bytes`
      var alignSize =
        ((~(_bb.LongLength - Space + size)) + 1) &
        (align - 1);
      // Reallocate the buffer if needed.
      var needed = alignSize + align + size;
      if (Space < needed)
        GrowBuffer(needed);
      if (alignSize > 0)
        Pad(alignSize);
    }

    public ulong Put<T>(T x)
      => Offset += _bb.Put(Offset, x);

    public ulong Put<T>(in T x)
      => Offset += _bb.Put(Offset, in x);

    /// <summary>
    /// Puts an array of type <typeparamref name="T"/> into this builder at the
    /// current offset
    /// </summary>
    /// <typeparam name="T">The type of the input data </typeparam>
    /// <param name="x">The array to copy data from</param>
    public ulong Put<T>(T[] x)
      where T : unmanaged
      => Offset += _bb.Put(Offset, x);

    /// <summary>
    /// Puts a span of type <typeparamref name="T"/> into this builder at the
    /// current offset
    /// </summary>
    /// <typeparam name="T">The type of the input data </typeparam>
    /// <param name="x">The span to copy data from</param>
    public ulong Put<T>(ReadOnlySpan<T> x)
      where T : unmanaged
      => Offset += _bb.Put<T>(Offset, x);

    /// <summary>
    /// Puts a span of type <typeparamref name="T"/> into this builder at the
    /// current offset
    /// </summary>
    /// <typeparam name="T">The type of the input data </typeparam>
    /// <param name="x">The span to copy data from</param>
    public ulong Put<T>(ReadOnlyBigSpan<T> x)
      => Offset += _bb.Put(Offset, x);

    public ulong Add<T>(T x) where T : unmanaged
    {
      Prep(ByteBuffer.SizeOf<T>(), 0);
      return Put(x);
    }

    public ulong Add<T>(T[] x)
      where T : unmanaged
    {
      if (x is null)
        throw new ArgumentNullException(nameof(x), "Cannot add a null array");

      if (x.Length == 0)
        // don't do anything if the array is empty
        return 0;

      var size = ByteBuffer.SizeOf<T>();
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
      var size = ByteBuffer.SizeOf<T>();
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
      var size = ByteBuffer.SizeOf<T>();
      // Need to prep on size (for data alignment) and then we pass the
      // rest of the length (minus 1) as additional bytes
      Prep(size, size * ((ulong)x.Length - 1));
      return Put(x);
    }


    /// <summary>
    /// Adds an offset, relative to where it will be written.
    /// </summary>
    /// <param name="offset">The offset to add to the buffer.</param>
    public ulong AddSignedOffset(ulong offset)
    {
      Prep(sizeof(ulong), sizeof(ulong)); // Ensure alignment is already done.
      if (offset > Offset)
        throw new ArgumentOutOfRangeException();
      offset = Offset - offset;
      return Put(offset);
    }

    public void StartVector(ulong elemSize, ulong count)
    {
      NotNested();
      Prep(sizeof(ulong), elemSize * count);
      PushVectorStart(Offset, elemSize);
      Put(count);
    }

    /// <summary>
    /// Writes data necessary to finish a vector construction.
    /// </summary>
    public VectorOffset EndVector(uint alignment = 8)
    {
      var start = PopVectorStart(out var elemSize);
      var len = _bb.Get<ulong>(start);
      var expected = start + 8 + len * elemSize;
      if (Offset != expected)
        throw new InvalidOperationException($"Incomplete vector, off by {(long)(expected - Offset)} bytes.");
      Prep(alignment, 0);
      return new(start);
    }

    /// <summary>
    /// Creates a vector of tables.
    /// </summary>
    /// <param name="offsets">Offsets of the tables.</param>
    public VectorOffset CreateVectorOfTables<T>(Offset<T>[] offsets)
      where T : struct, IBigBufferTable
    {
      NotNested();
      StartVector(sizeof(ulong), (ulong)offsets.LongLength);
      for (var i = offsets.LongLength - 1; i >= 0; i--) AddSignedOffset(offsets[i].Value);
      return EndVector();
    }

    public void Nested(ulong obj)
    {
      // Structs are always stored inline, so need to be created right
      // where they are used. You'll get this assert if you created it
      // elsewhere.
      if (obj != Offset)
        throw new("BigBuffers: unmanaged must be serialized inline.");
    }

    public void NotNested()
    {
      // You should not be creating any other objects or strings/vectors
      // while an object is being constructed
      if (_tableStarted)
        throw new("BigBuffers: object serialization must not be nested.");
    }

    public void StartTable(uint numFields)
    {
      NotNested();

      if (_vtable.Length < numFields)
        _vtable = new ulong[numFields];

      _vtableUsed = numFields;

      // Write placeholder for vtable offset
      Add(PlaceholderOffset);
      _tableStart = Offset - sizeof(ulong);
      _tableStarted = true;
    }

    /// <summary>
    /// Set the current vtable at <paramref name="vOffset"/> to the current location in the
    /// buffer.
    /// </summary>
    public void Slot(ulong vOffset, ulong size = 0)
    {
      if (vOffset >= _vtableUsed)
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
    /// Adds a buffer offset to the Table at index <paramref name="offset"/>
    /// in its vtable using the value <paramref name="value"/> and default <paramref name="defaultValue"/>
    /// </summary>
    /// <param name="offset">The index into the vtable</param>
    /// <param name="value">The value to put into the buffer. If the value is equal to the default
    /// the value will be skipped.</param>
    /// <param name="defaultValue">The default value to compare the value against</param>
    public void AddOffset(ulong offset, ulong value, ulong defaultValue)
      => Add(offset, Offset - value, defaultValue);

    /// <summary>
    /// Encode the string <paramref name="value"/> in the buffer using UTF-8.
    /// </summary>
    /// <param name="value">The string to encode.</param>
    /// <returns>
    /// The offset in the buffer where the encoded string starts.
    /// </returns>
    public StringOffset WriteString(string value)
    {
      if (value is null)
        return new(0);
      NotNested();
      var strLen = (ulong)Encoding.UTF8.GetByteCount(value);
      NotNested();
      Prep(sizeof(ulong), strLen + 1);
      var start = Offset;
      Put(strLen);
      Offset += _bb.PutStringUtf8(Offset, strLen, value);
      Add<byte>(0);
#if DEBUG
      var expected = start + 8 + strLen + 1;
      if (Offset != expected)
        throw new InvalidOperationException($"Incorrect string ending, off by {(long)(expected - Offset)} bytes.");
#endif
      //Prep(8, 0);
      return new(start);
    }

    public StringOffset MarkStringPlaceholder(out Placeholder placeholder)
    {
      placeholder = new(this, Offset);
      return new(PlaceholderOffset);
    }

    public VectorOffset MarkVectorPlaceholder(out Placeholder placeholder)
    {
      placeholder = new(this, Offset);
      return new(PlaceholderOffset);
    }

    public Offset<T> MarkOffsetPlaceholder<T>(out Placeholder<T> placeholder)
      where T : struct, IBigBufferEntity
    {
      placeholder = new(this, Offset);
      return new(PlaceholderOffset);
    }

    public StringOffset AddStringPlaceholder(out Placeholder placeholder)
    {
      placeholder = new(this, Offset);
      Prep(sizeof(ulong), sizeof(ulong));
      Put(PlaceholderOffset);
      return new(PlaceholderOffset);
    }

    public VectorOffset AddVectorPlaceholder(out Placeholder placeholder)
    {
      placeholder = new(this, Offset);
      Prep(sizeof(ulong), sizeof(ulong));
      Put(PlaceholderOffset);
      return new(PlaceholderOffset);
    }

    public Offset<T> AddOffsetPlaceholder<T>(out Placeholder<T> placeholder)
      where T : struct, IBigBufferEntity
    {
      placeholder = new(this, Offset);
      Prep(sizeof(ulong), sizeof(ulong));
      Put(PlaceholderOffset);
      return new(PlaceholderOffset);
    }

    /// <summary>
    /// Creates a string in the buffer from a Span containing
    /// a UTF8 string.
    /// </summary>
    /// <param name="chars">the UTF8 string to add to the buffer</param>
    /// <returns>
    /// The offset in the buffer where the encoded string starts.
    /// </returns>
    public StringOffset WriteString(BigSpan<byte> chars)
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
    public StringOffset WriteSharedString(string s)
      => s is null ? new(0) : _sharedStringMap.GetOrAdd(s, WriteString);

    // Structs are stored inline, so nothing additional is being added.
    // `d` is always 0.
    public void AddStruct(ulong size, ulong vOffset, ulong x, ulong d)
    {
      if (x == d) return;

      Nested(x + size);
      Slot(vOffset, size);
    }

    private void FinishVTable()
    {
      // clear vtable
      Unsafe.InitBlock(
        ref Unsafe.As<ulong, byte>(ref _vtable[0]), 0,
        (uint)(_vtableUsed * sizeof(ulong)));

      _vtableUsed = 0;
      _tableStarted = false;
    }

    public ulong EndTable()
    {

      if (!_tableStarted)
        throw new InvalidOperationException
          ("BigBuffers: calling EndTable without a StartTable");

      var paddingBytes = sizeof(ulong) - (Offset & (sizeof(ulong) - 1));
      if (paddingBytes < sizeof(ulong))
        Pad(paddingBytes);

      var vtableStart = Offset;

      var trimmedSize = _vtableUsed;

      // Trim trailing empty fields.
      while (trimmedSize > 0 && _vtable[trimmedSize - 1] == 0)
        trimmedSize--;

      _vtableUsed = trimmedSize;

      if (UseExistingVTables)
      {
        // Search for an existing vtable that matches the current one.

        var existingVtable = FindExistingVTable();

        if (existingVtable != 0)
        {
          // Found a match:
          // Remove the current vtable.
          Offset = vtableStart;
          // Point table to existing vtable.
          _bb.Put(_tableStart, _tableStart - existingVtable);

          FinishVTable();

          return _tableStart;
        }
      }

      // No match:
      // Write the vtable and add the location of the current vtable
      // to the list of vtables.

      var vtableSize = (ushort)((_vtableUsed + 2) * sizeof(ushort));
      Add(vtableSize);

      var tableSize = (ushort)(vtableStart - _tableStart);
      Add(tableSize);

      if (_vtableUsed > 0)
        for (var slot = 0uL; slot < _vtableUsed; slot++)
        {
          // Offset relative to the start of the table.
          var address = _vtable[slot];
          var shortOffset = checked(
            (ushort)(
              address != 0
                ? address - _tableStart
                : 0
            )
          );
          Add(shortOffset);
        }

      _writtenVTables.Add(vtableStart);
      // Point table to current vtable.
      _bb.Put(_tableStart, _tableStart - vtableStart);

      FinishVTable();

      return _tableStart;
    }

    private ulong FindExistingVTable()
    {

      foreach (var vtable in _writtenVTables)
      {
        if (CheckExistingVTable(vtable))
          return vtable;
      }

      return 0;
    }

    private bool CheckExistingVTable(ulong vtable)
    {
      const int vtableFieldsOffset = sizeof(ushort) * 2;
      var existingVTableSlots = (ulong)(_bb.Get<ushort>(vtable) - vtableFieldsOffset) / sizeof(ushort);

      if (existingVTableSlots != _vtableUsed)
        return false;

      for (ulong slot = 0; slot < existingVTableSlots; ++slot)
      {
        var newFieldOffset = _vtable[slot];

        var newFieldValue = checked(
          (ushort)(
            newFieldOffset != 0
              ? newFieldOffset - _tableStart
              : 0
          )
        );

        var existingFieldValue = _bb.Get<ushort>(vtable + vtableFieldsOffset + slot * 2);

        if (newFieldValue != existingFieldValue)
          return false;
      }
      return true;
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

    /// <summary>
    /// Begins a buffer, pointing to the given `root_table`.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    [DebuggerStepThrough]
    public void Begin()
      => Begin(false);

    /// <summary>
    /// Begins a buffer, pointing to the given `root_table`, with the size prefixed.
    /// </summary>
    /// <param name="rootTable">
    /// An offset to be added to the buffer.
    /// </param>
    [DebuggerStepThrough]
    public void BeginSizePrefixed()
      => Begin(true);

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
    /// Begins a buffer, pointing to the given `rootTable`.
    /// </summary>
    /// <param name="fileIdentifier">
    /// A BigBuffer file identifier to be added to the buffer before
    /// `root_table`.
    /// </param>
    /// <param name="sizePrefix">
    /// Whether to prefix the size to the buffer.
    /// </param>
    protected void Begin(string fileIdentifier, bool sizePrefix)
    {
      if (fileIdentifier.Length > Constants.FileIdentifierLength)
        throw new ArgumentException(
          $"BigBuffers: file identifier must be less than {Constants.FileIdentifierLength} bytes",
          nameof(fileIdentifier));

      var sizePrefixSize = sizePrefix ? sizeof(ulong) : 0uL;
      var sizeOfHeader = sizeof(ulong) + sizePrefixSize + Constants.FileIdentifierLength;

      Prep(sizeof(ulong), sizeOfHeader);
      Add(Offset + sizeOfHeader);
      if (sizePrefix)
      {
        _sizePrefixOffset = Offset;
        Add(PlaceholderOffset);
      }
      {
        // scope for i
        var i = 0;
        for (; i < fileIdentifier.Length; i++)
          Add((byte)fileIdentifier[i]);
        for (; i < Constants.FileIdentifierLength; i++)
          Add<byte>(0);
      }
      Prep(sizeof(ulong), 12);
    }

    protected void Begin(bool sizePrefix)
    {
      var sizePrefixSize = sizePrefix ? sizeof(ulong) : 0uL;
      var sizeOfHeader = sizeof(ulong) + sizePrefixSize;

      Prep(sizeof(ulong), sizeOfHeader);
      Add(Offset + sizeOfHeader);
      if (sizePrefix)
      {
        _sizePrefixOffset = Offset;
        Add(PlaceholderOffset);
      }
      Prep(sizeof(ulong), 12);
    }

    public void Finish()
    {
      if (_sizePrefixOffset != 0)
        throw new InvalidOperationException
          ("A size prefix was requested and thus must be provided upon finishing a buffer.");
    }
    public void FinishSizePrefixed()
    {
      if (_sizePrefixOffset == 0)
        throw new InvalidOperationException
          ("No size prefix was requested and thus must not be provided upon finishing a buffer.");

      ByteBuffer.Put(_sizePrefixOffset, Offset - _sizePrefixOffset);
    }

    /// <summary>
    /// Begins a buffer, pointing to the given `rootTable`.
    /// </summary>
    /// <param name="fileIdentifier">
    /// A BigBuffer file identifier to be added to the buffer before
    /// `root_table`.
    /// </param>
    [DebuggerStepThrough]
    public void Begin(string fileIdentifier)
      => Begin(fileIdentifier, false);

    /// <summary>
    /// Begins a buffer, pointing to the given `rootTable`, with the size prefixed.
    /// </summary>
    /// <param name="fileIdentifier">
    /// A BigBuffer file identifier to be added to the buffer before
    /// `root_table`.
    /// </param>
    [DebuggerStepThrough]
    public void BeginSizePrefixed(string fileIdentifier)
      => Begin(fileIdentifier, true);

    public void WriteOffset<T>(Offset<T> offset) where T : struct, IBigBufferEntity
    {
      Prep(sizeof(ulong), sizeof(ulong));
      Put(offset.Value - Offset);
    }

    public void WriteOffset(Offset offset)
    {
      Prep(sizeof(ulong), sizeof(ulong));
      Put(offset.Value - Offset);
    }

    public void WriteOffset(VectorOffset offset)
    {
      Prep(sizeof(ulong), sizeof(ulong));
      Put(offset.Value - Offset);
    }

    public void WriteOffset(StringOffset offset)
    {
      Prep(sizeof(ulong), sizeof(ulong));
      Put(offset.Value - Offset);
    }
  }
}
