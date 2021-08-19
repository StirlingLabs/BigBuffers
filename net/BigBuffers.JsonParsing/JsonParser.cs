#nullable enable
#define DEFERRED_IS_COMPLICATED
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JetBrains.Annotations;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Magic;
using static BigBuffers.JsonParsing.JsonParser;

namespace BigBuffers.JsonParsing
{
  internal delegate void PlaceholderArrayFiller<in T>(Placeholder placeholder, T[] array);

  internal delegate void PlaceholderSpanFiller<T>(Placeholder placeholder, ReadOnlyBigSpan<T> array);

  public static class JsonParser
  {
    internal const BindingFlags AnyAccessBindingFlags = BindingFlags.Public | BindingFlags.NonPublic;

    internal static readonly Dictionary<Type, MethodInfo> PrimitiveParsers = new()
    {
      { typeof(bool), ((Func<JsonElement, bool>)GetBoolean).Method },
      { typeof(byte), ((Func<JsonElement, byte>)GetSByte).Method },
      { typeof(sbyte), ((Func<JsonElement, sbyte>)GetByte).Method },
      { typeof(short), ((Func<JsonElement, short>)GetShort).Method },
      { typeof(ushort), ((Func<JsonElement, ushort>)GetUShort).Method },
      { typeof(int), ((Func<JsonElement, int>)GetInt).Method },
      { typeof(uint), ((Func<JsonElement, uint>)GetUInt).Method },
      { typeof(long), ((Func<JsonElement, long>)GetLong).Method },
      { typeof(ulong), ((Func<JsonElement, ulong>)GetULong).Method },
      { typeof(float), ((Func<JsonElement, float>)GetSingle).Method },
      { typeof(double), ((Func<JsonElement, double>)GetDouble).Method },
    };

    private static bool GetBoolean(JsonElement item) => item.GetBoolean();

    private static byte GetSByte(JsonElement item) => item.GetByte();

    private static sbyte GetByte(JsonElement item) => item.GetSByte();

    private static short GetShort(JsonElement item) => item.GetInt16();

    private static ushort GetUShort(JsonElement item) => item.GetUInt16();

    private static int GetInt(JsonElement item) => item.GetInt32();

    private static uint GetUInt(JsonElement item) => item.GetUInt32();

    private static long GetLong(JsonElement item) => item.GetInt64();

    private static ulong GetULong(JsonElement item) => item.GetUInt64();

    private static float GetSingle(JsonElement item) => item.GetSingle();

    private static double GetDouble(JsonElement item) => item.GetDouble();

    // TODO: follow and fix up calls
    internal static readonly MethodInfo BigBufferBuilderPutOffsetPlaceholderMethodInfo = typeof(BigBufferBuilder)
      .GetMethod(nameof(BigBufferBuilder.AddOffsetPlaceholder))!;

    internal static readonly MethodInfo ResolveEnumMethodInfo = typeof(JsonUtilities)
      .GetMethod(nameof(JsonUtilities.ResolveEnum))!;

    internal static readonly MethodInfo UnboxMethodInfo = typeof(Unsafe).GetMethod(nameof(Unsafe.Unbox))!;


#if !NETSTANDARD2_0
    [return: NotNullIfNotNull("value")]
#else
#pragma warning disable 8603
#endif
    internal static object? ReBox(object? value, Type type)
      => UnboxMethodInfo!.MakeGenericMethod(type)
        .Invoke(null, new[] { value });

    internal static object ReBoxToEnumType(long value, Type enumType)
    {
      var underlying = enumType.GetEnumUnderlyingType();
      if (underlying == typeof(sbyte))
        return ReBox((sbyte)value, enumType);
      if (underlying == typeof(byte))
        return ReBox((byte)value, enumType);
      if (underlying == typeof(short))
        return ReBox((short)value, enumType);
      if (underlying == typeof(ushort))
        return ReBox((ushort)value, enumType);
      if (underlying == typeof(int))
        return ReBox((int)value, enumType);
      if (underlying == typeof(uint))
        return ReBox((uint)value, enumType);
      if (underlying == typeof(long))
        return ReBox((long)value, enumType);
      if (underlying == typeof(ulong))
        return ReBox((ulong)value, enumType);
      throw new NotSupportedException(underlying.ToString());
    }

    internal static object ReBoxToEnumType(ulong value, Type enumType)
    {
      var underlying = enumType.GetEnumUnderlyingType();
      if (underlying == typeof(sbyte))
        return ReBox((sbyte)value, enumType);
      if (underlying == typeof(byte))
        return ReBox((byte)value, enumType);
      if (underlying == typeof(short))
        return ReBox((short)value, enumType);
      if (underlying == typeof(ushort))
        return ReBox((ushort)value, enumType);
      if (underlying == typeof(int))
        return ReBox((int)value, enumType);
      if (underlying == typeof(uint))
        return ReBox((uint)value, enumType);
      if (underlying == typeof(long))
        return ReBox((long)value, enumType);
      if (underlying == typeof(ulong))
        return ReBox((ulong)value, enumType);
      throw new NotSupportedException(underlying.ToString());
    }
#if NETSTANDARD2_0
#pragma warning restore 8603
#endif

    internal static readonly Type[] EntityCtorParamTypes = { typeof(ulong), typeof(ByteBuffer) };
    internal static readonly Type[] JsonParserCtorParamTypes = { typeof(IJsonParser) };

#if DEBUG
    internal static readonly Stack<(ulong, string)> OffsetStack = new();
    internal static void PushOffset(ulong offset, string name) => OffsetStack.Push((offset, name));
#endif
  }

  public sealed partial class JsonParser<T> : IJsonParser<T>
    where T : struct, IBigBufferEntity
  {
    public BigBufferBuilder Builder { get; }

    public static readonly Type Type = typeof(T);
    Type IJsonParser.Type => Type;

    // ReSharper disable once StaticMemberInGenericType
    private static readonly Type MetadataType = Type.GetNestedType("Metadata\u20f0")!;
    Type IJsonParser.MetadataType => MetadataType;

    // ReSharper disable once StaticMemberInGenericType
    private static readonly (string Name, bool Deprecated, ushort Offset, ushort Size, ushort Align, object Default)[] Metadata
      = ((string, bool, ushort, ushort, ushort, object)[])(MetadataType.GetField("Fields")?.GetValue(null)
        ?? throw new NotSupportedException($"{Type} is does not have sufficient metadata to support parsing."));


    // ReSharper disable once StaticMemberInGenericType
    public static readonly Action<BigBufferBuilder>? Starter =
      IfType<T>.IsAssignableTo<IBigBufferStruct>()
        ? null
        :
#if NETSTANDARD
        (Action<BigBufferBuilder>)
#endif
        Type.GetMethod("Start" + Type.Name,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!
#if NETSTANDARD
          .CreateDelegate(typeof(Action<BigBufferBuilder>));
#else
          .CreateDelegate<Action<BigBufferBuilder>>();
#endif

    // ReSharper disable once StaticMemberInGenericType
    public static readonly Func<BigBufferBuilder, Offset<T>>? Ender =
      IfType<T>.IsAssignableTo<IBigBufferStruct>()
        ? null
        :
#if NETSTANDARD
        (Func<BigBufferBuilder, Offset<T>>)
#endif
        Type.GetMethod("End" + Type.Name,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!
#if NETSTANDARD
          .CreateDelegate(typeof(Func<BigBufferBuilder, Offset<T>>));
#else
          .CreateDelegate<Func<BigBufferBuilder, Offset<T>>>();
#endif

    // ReSharper disable once StaticMemberInGenericType
    public static readonly MethodInfo? BufferBeginner
      = Type.GetMethod("Begin" + Type.Name + "Buffer",
        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly MethodInfo? BufferFinisher
      = Type.GetMethod("End" + Type.Name + "Buffer",
        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly MethodInfo? BufferBeginnerSizePrefixed
      = Type.GetMethod("BeginSizePrefixed" + Type.Name + "Buffer",
        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly MethodInfo? BufferFinisherSizePrefixed
      = Type.GetMethod("EndSizePrefixed" + Type.Name + "Buffer",
        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ulong ByteSize
      = IfType<T>.IsAssignableTo<IBigBufferTable>()
        ? 0
        : (ulong)(typeof(T)
          .GetProperty("ByteSize", BindingFlags.Public | BindingFlags.Static)!
          .GetValue(null)!);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableArray<MethodInfo> FieldAdders
      = IfType<T>.IsAssignableTo<IBigBufferStruct>()
        ? ImmutableArray<MethodInfo>.Empty
        : Type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
          .Where(m => m.Name.StartsWith("Add"))
          .ToImmutableArray();

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableArray<MethodInfo> FieldSetters
      = IfType<T>.IsAssignableTo<IBigBufferTable>()
        ? ImmutableArray<MethodInfo>.Empty
        : Type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .Where(m => m.Name.StartsWith("Set") || m.Name.StartsWith("set_"))
          .ToImmutableArray();

    public static readonly ImmutableArray<PropertyInfo> StructFieldProperties
      = IfType<T>.IsAssignableTo<IBigBufferTable>()
        ? ImmutableArray<PropertyInfo>.Empty
        : Type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
          .Where(m => typeof(IBigBufferStruct).IsAssignableFrom(m.PropertyType))
          .ToImmutableArray();

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<ushort, MethodInfo> FieldIndexToAdder
      = FieldAdders.ToImmutableDictionary(m => m.GetCustomAttribute<MetadataIndexAttribute>()!.Index);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<ushort, MethodInfo> FieldIndexToSetter
      = FieldSetters.ToImmutableDictionary(m => m.GetCustomAttribute<MetadataIndexAttribute>()!.Index);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<ushort, MethodInfo> StructFieldsIndex
      = StructFieldProperties.ToImmutableDictionary(
        m => m.GetCustomAttribute<MetadataIndexAttribute>()!.Index,
        m => m.GetGetMethod(false)!);

    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<string, ushort> FieldNameToIndex
      = FieldAdders.Select(m => m.GetCustomAttribute<MetadataIndexAttribute>()!.Index)
        .ToImmutableDictionary(i => Metadata[i].Name);


    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<ushort, MethodInfo> FieldVectorStarters
      = Type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(m => m.Name.StartsWith("Start") && m.Name.EndsWith("Vector"))
        .ToImmutableDictionary(m => m.GetCustomAttribute<MetadataIndexAttribute>()!.Index);


    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<ushort, MethodInfo[]> FieldVectorFillers
      = Type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(m => m.Name.StartsWith("Fill") && m.Name.EndsWith("Vector"))
        .Select(m => (m.GetCustomAttribute<MetadataIndexAttribute>()!.Index, Method: m))
        .GroupBy(kv => kv.Index, kv => kv.Method)
        .ToImmutableDictionary(g => g.Key, g => g.ToArray());


    // ReSharper disable once StaticMemberInGenericType
    public static readonly ImmutableDictionary<ushort, MethodInfo> FieldVectorCreators
      = Type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(m => m.Name.StartsWith("Create") && m.Name.EndsWith("Vector"))
        .ToImmutableDictionary(m => m.GetCustomAttribute<MetadataIndexAttribute>()!.Index);

    public ref readonly (string Name, bool Deprecated, ushort Offset, ushort Size, ushort Align, object Default)
      GetMetadata(ulong index) => ref Metadata[index];

    public readonly Dictionary<ushort, object> UnionTypeSpecs = new();


    internal void StoreUnionTypeSpec<TEnum>(ushort fieldIndex, TEnum value)
      where TEnum : unmanaged, Enum
      => UnionTypeSpecs[fieldIndex] = value;

    internal static readonly MethodInfo StoreUnionTypeSpecMethodInfo = typeof(JsonParser<T>)
      .GetMethod(nameof(StoreUnionTypeSpec), AnyAccessBindingFlags | BindingFlags.Instance)!;

    internal TEnum GetUnionTypeSpec<TEnum>(ushort fieldIndex)
      where TEnum : unmanaged, Enum
      => (TEnum)UnionTypeSpecs[(ushort)(fieldIndex - 1)];

    internal static readonly MethodInfo GetUnionTypeSpecMethodInfo = typeof(JsonParser<T>)
      .GetMethod(nameof(GetUnionTypeSpec), AnyAccessBindingFlags | BindingFlags.Instance)!;
    internal bool IsInline { get; }
    bool IJsonParser.IsInline => IsInline;

    private readonly bool _hasParent;

    internal IProducerConsumerCollection<Action> DeferredActions { get; }

    IProducerConsumerCollection<Action> IJsonParser.DeferredActions => DeferredActions;

#if DEBUG
    public List<Expression> ExpressionsTaken { get; }
#endif

    // ReSharper disable once StaticMemberInGenericType
    internal static readonly Dictionary<string, Action<JsonParser<T>, JsonElement>> FieldParsers = new();

#if DEBUG
    internal static readonly Dictionary<string, Expression<Action<JsonParser<T>, JsonElement>>>
      FieldParserExpressions = new();
#endif

    public JsonParser(BigBufferBuilder builder)
    {
      Builder = builder;
      IsInline = typeof(IBigBufferStruct).IsAssignableFrom(typeof(T));
      _hasParent = false;
#if DEFERRED_IS_QUEUE
      DeferredActions = new ConcurrentQueue<Action>();
#elif DEFERRED_IS_STACK
      DeferredActions = new ConcurrentStack<Action>();
#elif DEFERRED_IS_BAG
      DeferredActions = new ConcurrentBag<Action>();
#elif DEFERRED_IS_COMPLICATED
      // these get pulled into the closures, so lifetime is maintained properly
      IProducerConsumerCollection<Action> elements = new ConcurrentQueue<Action>();
      IProducerConsumerCollection<Action> strings = new ConcurrentQueue<Action>();
      MethodInfo? parseAndFillStringLambda = null;
      DeferredActions = DeferredProducerConsumerCollection.Create(
        (in Action a) => {
          var method = a.Method;

          // diagnose strings
          if (parseAndFillStringLambda is null)
          {
            if (method.Name.Contains("ParseAndFillString"))
            {
              parseAndFillStringLambda = method;
              return strings.TryAdd(a);
            }
          }
          else if (method == parseAndFillStringLambda)
            return strings.TryAdd(a);

          // all others
          return elements.TryAdd(a);
        },
        (out Action a) =>
          elements.TryTake(out a!)
          || strings.TryTake(out a!),
        false
      );
#endif
#if DEBUG
      ExpressionsTaken = new();
#endif
      Entity = new();
      if (IsInline)
      {
        var s = (IBigBufferStruct)Entity;
        builder.Prep(s.Alignment, s.ByteSize);
      }
      Entity.Model = new(builder.ByteBuffer, builder.Offset);
    }

    internal JsonParser(IJsonParser parent)
    {
      Builder = parent.Builder;
      IsInline = parent.IsInline ||
        typeof(IBigBufferStruct).IsAssignableFrom(typeof(T));
      _hasParent = true;
      DeferredActions = parent.DeferredActions;
#if DEBUG
      Parent = parent;
      ExpressionsTaken = parent.ExpressionsTaken;
#endif
      Entity = new();
      if (IsInline)
      {
        var s = (IBigBufferStruct)Entity;
        Builder.Prep(s.Alignment, s.ByteSize);
      }
      else
      {
        Builder.Prep(sizeof(ulong), 0);
      }
      Entity.Model = new(Builder.ByteBuffer, Builder.Offset);
    }

    internal JsonParser(IJsonParser parent, T entity)
    {
      Builder = parent.Builder;
      IsInline = parent.IsInline ||
        typeof(IBigBufferStruct).IsAssignableFrom(typeof(T));
      _hasParent = true;
      DeferredActions = parent.DeferredActions;
#if DEBUG
      Parent = parent;
      ExpressionsTaken = parent.ExpressionsTaken;
#endif
      Entity = entity;
    }

#if DEBUG
    internal IJsonParser? Parent { get; }
    IJsonParser? IJsonParser.Parent => Parent;
#endif

    internal void ParseAndFillString(JsonElement item, Placeholder placeholder)
      => DeferredActions.TryAdd(() => placeholder.Fill(item.GetString()));

    internal static readonly MethodInfo ParseAndFillStringMethodInfo
      = typeof(JsonParser<T>).GetMethod(nameof(ParseAndFillString), AnyAccessBindingFlags | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;

    static JsonParser()
    {
      if (IfType<T>.IsAssignableTo<IBigBufferTable>())
        CreateTableFieldParsers();
      else
        CreateStructFieldParsers();
    }

    [UsedImplicitly]
    internal T Entity;

    T IJsonParser<T>.Entity => Entity;

    IBigBufferEntity IJsonParser.Entity => Entity;

    private static Expression DescendPropertiesOrFields(Expression expr, params string[] descendants)
    {
      foreach (var descendant in descendants)
        expr = Expression.PropertyOrField(expr, descendant);
      return expr;
    }

    [Conditional("DEBUG")]
    internal static void OffsetAssert(
      string fieldName,
      ulong offset,
      ulong fieldSize,
      ulong currentOffset
    )
    {
      var sizeDiff = (long)(currentOffset - (fieldSize + offset));
      if (sizeDiff != 0)
        throw new(
          $"Parsing {typeof(T).FullName}.{fieldName}: Started at {offset}, expected +{fieldSize} bytes, at {currentOffset} (off by {sizeDiff}).");
    }

#if DEBUG
    [Conditional("DEBUG")]
    internal static void StructOffsetAssert(
      IJsonParser parser,
      string fieldName,
      ulong offset,
      ulong fieldSize,
      ulong currentOffset,
      ulong fieldOffset
    )
    {
      Debug.Assert(parser.IsInline);
      var entityOffset = parser.Entity.Model.Offset;
      var parentParser = parser.Parent;
      Debug.Assert(parentParser != null, nameof(parentParser) + " != null");

      if (!parentParser!.IsInline) return;

      var parentEntity = parentParser.Entity;
      var parentOffset = parentEntity.Model.Offset;
      var parentStruct = (IBigBufferStruct)parentEntity;

      var endOfParent = parentOffset + parentStruct.ByteSize;

      Debug.Assert(entityOffset >= parentOffset);
      Debug.Assert(entityOffset + fieldOffset >= parentOffset);

      Debug.Assert(entityOffset <= endOfParent);
      Debug.Assert(entityOffset + fieldSize <= endOfParent);
    }

    [Conditional("DEBUG")]
    internal void Peek(params object[] args)
    {
      // nothing to see here
    }
#endif

    public T Parse(JsonElement element)
    {
#if DEBUG
      var unfilledCount = 0;
      if (!_hasParent)
        Placeholder.GetUnfilledCount(Builder, out unfilledCount);
#endif
      try
      {
        if (IfType<T>.IsAssignableTo<IBigBufferTable>())
        {
          Starter!(Builder);
          foreach (var prop in element.EnumerateObject())
          {
            if (!FieldParsers.TryGetValue(prop.Name, out var parser))
              throw new NotImplementedException();
#if DEBUG
            ExpressionsTaken.Add(FieldParserExpressions[prop.Name]);
#endif
            parser(this, prop.Value);
          }
          Ender!(Builder);
        }
        else if (IfType<T>.IsAssignableTo<IBigBufferStruct>())
        {
          foreach (var prop in element.EnumerateObject())
          {
            if (!FieldParsers.TryGetValue(prop.Name, out var parser))
              throw new NotImplementedException();
#if DEBUG
            ExpressionsTaken.Add(FieldParserExpressions[prop.Name]);
#endif
            parser(this, prop.Value);
          }
        }
        else
        {
          throw new NotImplementedException();
        }

        if (!_hasParent)
        {
          while (DeferredActions.TryTake(out var filler))
            filler();

#if DEBUG
          Placeholder.ValidateUnfilledCount(Builder, unfilledCount);
#endif
        }
      }
      finally
      {
        if (!_hasParent)
        {
#if NETSTANDARD2_0
          // @formatter:off
          while (DeferredActions.TryTake(out _)) { /* clearing */ }
          // @formatter:on
#else
          switch (DeferredActions)
          {
            // @formatter:off
            case ConcurrentQueue<Action> cq: cq.Clear(); break;
            case ConcurrentStack<Action> cs: cs.Clear(); break;
            case ConcurrentBag<Action> cb: cb.Clear(); break;
            default: while (DeferredActions.TryTake(out _)) { /* clearing */ } break;
            // @formatter:on
          }
#endif
        }
      }

      return Entity;
    }
  }
}
