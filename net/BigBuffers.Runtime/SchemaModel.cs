using System;
using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;

namespace BigBuffers
{
  public static class SchemaModel
  {
    internal static ref Model Model<TModel>(ref this TModel model)
      where TModel : struct, IBigBufferEntity
      => ref model.Model;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly Model __init(ref this Model bb, ulong table, in ByteBuffer buffer)
    {
      bb = new(buffer, table);
      return ref bb;
    }

    public static ref readonly TModel __assign<TModel>(ref this TModel model, ulong table, in ByteBuffer buffer)
      where TModel : struct, IBigBufferEntity
    {
      model.Model.__init(table, buffer);
      return ref model;
    }

    // Look up a field in the vtable, return an offset into the object, or 0 if the field is not
    // present.

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vtable(ref this Model model)
    {
      ref readonly var bb = ref model.ByteBuffer;
      ref readonly var table = ref model.Offset;
      var vtable = table - bb.Get<ulong>(table);
      return vtable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __offset(ref this Model model, ulong vtableOffset)
    {
      ref readonly var bb = ref model.ByteBuffer;
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
    public static ulong __indirect(ref this Model bb, ulong offset)
      => bb.ByteBuffer.__indirect(offset);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __indirect(in this ByteBuffer bb, ulong offset)
      => offset + bb.Get<ulong>(offset);

    // Create a .NET String from UTF-8 data stored inside the flatbuffer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string __string(ref this Model model, ulong offset)
    {
      ref readonly var bb = ref model.ByteBuffer;
      //ref readonly var table = ref model.Offset;
      var field = offset;
      var indirection = field + bb.Get<ulong>(field);
      var strLen = bb.Get<ulong>(indirection);
      var strStart = indirection + sizeof(ulong);
      return bb.GetStringUtf8(strStart, (int)strLen);
    }

    // Get the length of a vector whose offset is stored at "offset" in this object.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vector_len(ref this Model model, ulong offset)
    {
      ref readonly var bb = ref model.ByteBuffer;
      ref readonly var table = ref model.Offset;
      var field = offset + table;
      var indirection = field + bb.Get<ulong>(field);
      return bb.Get<ulong>(indirection);
    }

    // Get the start of data of a vector whose offset is stored at "offset" in this object.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong __vector(ref this Model model, ulong offset)
    {
      ref readonly var bb = ref model.ByteBuffer;
      ref readonly var table = ref model.Offset;
      var field = offset + table;
      var indirection = field + bb.Get<ulong>(field);
      var startPos = indirection + sizeof(ulong);
      return startPos;
    }

    // Initialize any Table-derived type to point to the union at the given offset.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult __union<TResult>(ref this Model bb, ulong offset)
      where TResult : struct, IBigBufferEntity
    {
      var t = new TResult();
      var indirect = bb.__indirect(offset);
      t.Model.__init(indirect, bb.ByteBuffer);
      return t;
    }


    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // ArraySegment&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then a null value will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArraySegment<byte>? __vector_as_arraysegment(ref this Model model, ulong offset)
    {
      ref readonly var bb = ref model.ByteBuffer;
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
    public static T[] __vector_as_array<T>(ref this Model model, ulong offset)
      where T : unmanaged
    {
      ref readonly var bb = ref model.ByteBuffer;
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


    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then an empty span will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<T> __vector_as_span<T>(ref this Model model, ulong offset, ulong elementSize)
    {
      ref readonly var bb = ref model.ByteBuffer;
      if (!BitConverter.IsLittleEndian)
        throw new NotSupportedException("Getting typed span on a Big Endian " +
          "system is not support");

      var o = model.__offset(offset);
      if (0 == o) return default;

      var pos = model.__vector(o);
      var len = model.__vector_len(o);
      return bb.ToSpan(pos, len * elementSize).CastAs<T>();
    }


    // Get the data of a vector whoses offset is stored at "offset" in this object as an
    // Spant&lt;byte&gt;. If the vector is not present in the ByteBuffer,
    // then an empty span will be returned.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BigSpan<byte> __vector_as_byte_span(ref this Model model, ulong offset, ulong elementSize)
    {
      ref readonly var bb = ref model.ByteBuffer;
      if (!BitConverter.IsLittleEndian)
        throw new NotSupportedException("Getting typed span on a Big Endian " +
          "system is not support");

      var o = model.__offset(offset);
      if (0 == o) return default;

      var pos = model.__vector(o);
      var len = model.__vector_len(o);
      return bb.ToSpan(pos, len * elementSize).CastAs<byte>();
    }

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
        if (bb.GetByte(bbPos + i) != 0)
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
