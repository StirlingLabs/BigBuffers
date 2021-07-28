using System;
using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public static class SchemaModel
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T __init<T, TModel>(ref this T obj, ulong table, ByteBuffer buffer)
      where TModel : struct, ISchemaModel
      where T : struct, IBigBufferModel<TModel>
    {
      ref var model = ref obj.Model;
      model.ByteBufferOffset = new(buffer, table);
      return ref obj;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T __init<T>(ref this T obj, ulong table, ByteBuffer buffer)
      where T : struct, IBigBufferTable
      => ref obj.__init<T, Table>(table, buffer);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T __assign<T>(this T obj, ulong table, ByteBuffer buffer)
      where T : struct, IBigBufferStruct
      => obj.__init<T, Struct>(table, buffer);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once MethodOverloadWithOptionalParameter
    public static T __assign<T>(this T obj, ulong table, ByteBuffer buffer, bool _ = false)
      where T : struct, IBigBufferTable
      => obj.__init<T, Table>(table, buffer);


    // Look up a field in the vtable, return an offset into the object, or 0 if the field is not
    // present.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vtable<TModel>(ref this TModel model) where TModel : struct, ISchemaTable
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      ref readonly var table = ref model.ByteBufferOffset.Offset;
      var vtable = table - bb.Get<ulong>(table);
      return vtable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __offset<TModel>(ref this TModel model, ulong vtableOffset)
      where TModel : struct, ISchemaTable
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      var vtable = __vtable(ref model);
      if (FieldWasTrimmed(bb, vtable, vtableOffset)) return 0;
      var offset = bb.Get<ushort>(vtable + vtableOffset);
      return offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __offset(ulong vtableOffset, ulong table, in ByteBuffer bb)
    {
      // TODO: verify
      var vtable = bb.Get<ulong>(table);
      if (FieldWasTrimmed(bb, vtable, vtableOffset)) return 0;
      return bb.Get<ushort>(table + vtableOffset - vtable) + table;
    }

    private static bool FieldWasTrimmed(in ByteBuffer bb, ulong vtable, ulong offset)
      => offset > bb.Get<ushort>(vtable);

    // Retrieve the relative offset stored at "offset"
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __indirect<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
      => __indirect(offset, model.ByteBufferOffset.ByteBuffer);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __indirect(ulong offset, in ByteBuffer bb)
      => offset + bb.Get<ulong>(offset);

    // Create a .NET String from UTF-8 data stored inside the flatbuffer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string __string<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      ref readonly var table = ref model.ByteBufferOffset.Offset;
      var field = offset + table;
      var indirection = field + bb.Get<ulong>(field);
      var strLen = bb.Get<ulong>(indirection);
      var strStart = indirection + sizeof(ulong);
      return bb.GetStringUtf8(strStart, (int)strLen - 1);
    }

    // Get the length of a vector whose offset is stored at "offset" in this object.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vector_len<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      ref readonly var table = ref model.ByteBufferOffset.Offset;
      var field = offset + table;
      var indirection = field + bb.Get<ulong>(field);
      return bb.Get<ulong>(indirection);
    }

    // Get the start of data of a vector whose offset is stored at "offset" in this object.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vector<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      ref readonly var table = ref model.ByteBufferOffset.Offset;
      var field = offset + table;
      var indirection = field + bb.Get<ulong>(field);
      var startPos = indirection + sizeof(ulong);
      return startPos;
    }

    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then an empty span will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<T> __vector_as_span<TModel, T>(ref this TModel model, ulong offset, ulong elementSize)
      where T : unmanaged where TModel : struct, ISchemaTable
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      if (!BitConverter.IsLittleEndian)
        throw new NotSupportedException("Getting typed span on a Big Endian " +
          "system is not support");

      var o = model.__offset(offset);
      if (0 == o) return default;

      var pos = model.__vector(o);
      var len = model.__vector_len(o);
      return bb.ToSpan(pos, len * elementSize).CastAs<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<T> __vector_as_span<T>(ref this Table model, ulong offset, ulong elementSize) where T : unmanaged
      => model.__vector_as_span<Table, T>(offset, elementSize);


    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then an empty span will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<byte> __vector_as_byte_span<TModel>(ref this TModel model, ulong offset, ulong elementSize)
      where TModel : struct, ISchemaTable
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      if (!BitConverter.IsLittleEndian)
        throw new NotSupportedException("Getting typed span on a Big Endian " +
          "system is not support");

      var o = model.__offset(offset);
      if (0 == o) return default;

      var pos = model.__vector(o);
      var len = model.__vector_len(o);
      return bb.ToSpan(pos, len * elementSize).CastAs<byte>();
    }

    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // ArraySegment&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then a null value will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArraySegment<byte>? __vector_as_arraysegment<TModel>(ref this TModel model, ulong offset)
      where TModel : struct, ISchemaTable
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      var o = model.__offset(offset);
      if (0 == o) return null;

      var pos = model.__vector(o);
      var len = model.__vector_len(o);
      return bb.ToArraySegment(checked((int)pos), checked((int)len));
    }

    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // T[]. If the vector is not present in the ByteBuffer, then a null value will be
    // returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T[] __vector_as_array<TModel, T>(ref this TModel model, ulong offset)
      where TModel : struct, ISchemaTable
      where T : unmanaged
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      if (!BitConverter.IsLittleEndian)
        throw new NotSupportedException("Getting typed arrays on a Big Endian " +
          "system is not support");

      var o = model.__offset(offset);
      if (0 == o)
        return null;

      var pos = model.__vector(o);
      var len = model.__vector_len(o);
      return bb.ToArray<T>(pos, len);
    }
    public static T[] __vector_as_array<T>(ref this Table model, ulong offset) where T : unmanaged
      => model.__vector_as_array<Table, T>(offset);

    // Initialize any Table-derived type to point to the union at the given offset.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T __union<TModel, T>(ref this TModel model, ulong offset)
      where TModel : struct, ISchemaTable
      where T : struct, IBigBufferTable
    {
      ref readonly var bb = ref model.ByteBufferOffset.ByteBuffer;
      var t = new T();
      var indirect = model.__indirect(offset);
      t.__init(indirect, bb);
      return t;
    }
    public static T __union<T>(ref this Table model, ulong offset)
      where T : struct, IBigBufferTable
      => model.__union<Table, T>(offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool __has_identifier(in ByteBuffer bb, string ident)
    {
      if (ident.Length > Constants.FileIdentifierLength)
        throw new ArgumentException("BigBuffers: file identifier must be length " + Constants.FileIdentifierLength, nameof(ident));

      var bbPos = bb.Position + sizeof(ulong);
      ulong i;
      var identLength = (ulong)ident.Length;

      for (i = 0uL; i < identLength; i++)
      {
        if ((char)bb.GetByte(bbPos + i) != ident[(int)i])
          return false;
      }

      for (; i < Constants.FileIdentifierLength; i++)
      {
        if (bb.GetByte(bbPos + i) == 0)
          return false;
      }

      return true;
    }

    public static ref T ThrowNullRef<T>()
      => ref ThrowNullRef<T>("Member is defaulted and occupies no slot, nothing to reference.");

    public static ref T ThrowNullRef<T>(string msg)
      => throw new NullReferenceException(msg);

    // Compare strings in the ByteBuffer.
    public static int CompareStrings(ulong offset1, ulong offset2, ByteBuffer bb)
    {
      offset1 += bb.Get<ulong>(offset1);
      offset2 += bb.Get<ulong>(offset2);
      var len1 = bb.Get<ulong>(offset1);
      var len2 = bb.Get<ulong>(offset2);
      var startPos1 = offset1 + sizeof(long);
      var startPos2 = offset2 + sizeof(long);
      var len = (nuint)Math.Min(len1, len2);
      ref var start1 = ref bb.RefByte(startPos1);
      ref var start2 = ref bb.RefByte(startPos2);
      var c = ReadOnlyBigSpan.Create(ref start1, len)
        .CompareMemory(ReadOnlyBigSpan.Create(ref start2, len));
      if (c != 0) return c;
      return len1 < len2 ? -1 : len1 > len2 ? 1 : 0;
    }

    // Compare string from the ByteBuffer with the string object
    public static int CompareStrings(ulong offset1, byte[] key, ByteBuffer bb)
    {
      offset1 += bb.Get<ulong>(offset1);
      var len1 = bb.Get<ulong>(offset1);
      var len2 = (ulong)key.LongLength;
      var startPos1 = offset1 + sizeof(long);
      var len = (nuint)Math.Min(len1, len2);
      ref var start1 = ref bb.RefByte(startPos1);
      ref var start2 = ref key[0];
      var c = ReadOnlyBigSpan.Create(ref start1, len)
        .CompareMemory(ReadOnlyBigSpan.Create(ref start2, len));
      if (c != 0) return c;
      return len1 < len2 ? -1 : len1 > len2 ? 1 : 0;
    }
  }
}
