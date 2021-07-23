using System;
using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public static class SchemaModel
  {
    // Look up a field in the vtable, return an offset into the object, or 0 if the field is not
    // present.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __offset<TModel>(ref this TModel model, ulong vtableOffset) where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
      ref var table = ref model.ByteBufferOffset.Offset;
      var vtable = table - bb.Get<ulong>(table);
      var offset = bb.Get<ushort>(vtable + vtableOffset);
      if (offset == 0) return 0;
      return table + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __offset(ulong vtableOffset, ulong offset, ByteBuffer bb)
    {
      var vtable = bb.LongLength - offset;
      return bb.Get<ushort>(vtable + vtableOffset - bb.Get<ulong>(vtable)) + vtable;
    }

    // Retrieve the relative offset stored at "offset"
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __indirect<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
      return offset + bb.Get<ulong>(offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __indirect(ulong offset, ByteBuffer bb)
      => offset + bb.Get<ulong>(offset);

    // Create a .NET String from UTF-8 data stored inside the flatbuffer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string __string<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
      offset += bb.Get<ulong>(offset);
      var len = bb.Get<ulong>(offset);
      var startPos = offset + sizeof(ulong);
      return bb.GetStringUtf8(startPos, (int)len-1);
    }

    // Get the length of a vector whose offset is stored at "offset" in this object.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vector_len<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
      offset += model.ByteBufferOffset.Offset;
      offset += bb.Get<ulong>(offset);
      return bb.Get<ulong>(offset);
    }

    // Get the start of data of a vector whose offset is stored at "offset" in this object.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vector<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
      offset += bb.Get<ulong>(offset);
      var startPos = offset + sizeof(ulong);
      return startPos;
    }

    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then an empty span will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<T> __vector_as_span<TModel, T>(ref this TModel model, ulong offset, ulong elementSize)
      where T : unmanaged where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
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
    public static BigSpan<T> __vector_as_span<T>(ref this Struct model, ulong offset, ulong elementSize) where T : unmanaged
      => model.__vector_as_span<Struct, T>(offset, elementSize);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<T> __vector_as_span<T>(ref this Table model, ulong offset, ulong elementSize) where T : unmanaged
      => model.__vector_as_span<Table, T>(offset, elementSize);


    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then an empty span will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<byte> __vector_as_byte_span<TModel>(ref this TModel model, ulong offset, ulong elementSize)
      where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
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
    public static ArraySegment<byte>? __vector_as_arraysegment<TModel>(ref this TModel model, ulong offset) where TModel : struct, ISchemaModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
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
      where TModel : struct, ISchemaModel
      where T : unmanaged
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
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
    public static T[] __vector_as_array<T>(ref this Struct model, ulong offset) where T : unmanaged
      => model.__vector_as_array<Struct, T>(offset);
    public static T[] __vector_as_array<T>(ref this Table model, ulong offset) where T : unmanaged
      => model.__vector_as_array<Table, T>(offset);

    // Initialize any Table-derived type to point to the union at the given offset.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T __union<TModel, T>(ref this TModel model, ulong offset)
      where TModel : struct, ISchemaModel where T : struct, IBigBufferModel
    {
      ref var bb = ref model.ByteBufferOffset.ByteBuffer;
      var t = new T();
      t.__init(model.__indirect(offset), bb);
      return t;
    }

    public static T __union<T>(ref this Struct model, ulong offset) where T : struct, IBigBufferModel
      => model.__union<Struct, T>(offset);

    public static T __union<T>(ref this Table model, ulong offset) where T : struct, IBigBufferModel
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
