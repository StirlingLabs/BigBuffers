using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JetBrains.Annotations;
using StirlingLabs.Utilities.Magic;

namespace BigBuffers.JsonParsing
{
  [PublicAPI]
  [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
  internal sealed class JsonPrimitiveVectorParser<TModel, TVector> : IJsonVectorParser<TVector>
    where TModel : struct, IBigBufferEntity
    where TVector : unmanaged
  {
    private readonly JsonParser<TModel> _parser;
    private readonly Placeholder _placeholder;
    private readonly PlaceholderArrayFiller<TVector> _filler;
    public JsonPrimitiveVectorParser(JsonParser<TModel> parser, Placeholder placeholder, PlaceholderArrayFiller<TVector> filler)
    {
      _parser = parser;
      _placeholder = placeholder;
      _filler = filler;
    }

    // ReSharper disable once CognitiveComplexity
    public void Parse(JsonElement element)
    {
      var items = new TVector[element.GetArrayLength()];
      if (IfType<TVector>.Is<bool>()) ParseBooleans(element, items);
      else if (IfType<TVector>.Is<byte>()) ParseBytes(element, items);
      else if (IfType<TVector>.Is<sbyte>()) ParseSBytes(element, items);
      else if (IfType<TVector>.Is<short>()) ParseShorts(element, items);
      else if (IfType<TVector>.Is<ushort>()) ParseUShorts(element, items);
      else if (IfType<TVector>.Is<int>()) ParseInts(element, items);
      else if (IfType<TVector>.Is<uint>()) ParseUInts(element, items);
      else if (IfType<TVector>.Is<long>()) ParseLongs(element, items);
      else if (IfType<TVector>.Is<ulong>()) ParseULongs(element, items);
      else if (IfType<TVector>.Is<float>()) ParseSingles(element, items);
      else if (IfType<TVector>.Is<double>()) ParseDoubles(element, items);
      else throw new NotImplementedException(typeof(TVector).ToString());
      _parser.DeferredQueue.Enqueue(() => _filler(_placeholder, items));
    }
    private static void ParseBooleans(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetBoolean();
        items[++i] = Unsafe.As<bool, TVector>(ref v);
      }
    }
    private static void ParseBytes(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetByte();
        items[++i] = Unsafe.As<byte, TVector>(ref v);
      }
    }
    private static void ParseSBytes(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetSByte();
        items[++i] = Unsafe.As<sbyte, TVector>(ref v);
      }
    }
    private static void ParseShorts(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetInt16();
        items[++i] = Unsafe.As<short, TVector>(ref v);
      }
    }
    private static void ParseUShorts(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetUInt16();
        items[++i] = Unsafe.As<ushort, TVector>(ref v);
      }
    }
    private static void ParseInts(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetInt32();
        items[++i] = Unsafe.As<int, TVector>(ref v);
      }
    }
    private static void ParseUInts(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetUInt32();
        items[++i] = Unsafe.As<uint, TVector>(ref v);
      }
    }
    private static void ParseLongs(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetInt64();
        items[++i] = Unsafe.As<long, TVector>(ref v);
      }
    }
    private static void ParseULongs(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetUInt64();
        items[++i] = Unsafe.As<ulong, TVector>(ref v);
      }
    }
    private static void ParseSingles(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetSingle();
        items[++i] = Unsafe.As<float, TVector>(ref v);
      }
    }
    private static void ParseDoubles(JsonElement element, TVector[] items)
    {
      var i = -1;
      foreach (var item in element.EnumerateArray())
      {
        var v = item.GetDouble();
        items[++i] = Unsafe.As<double, TVector>(ref v);
      }
    }
  }
}
