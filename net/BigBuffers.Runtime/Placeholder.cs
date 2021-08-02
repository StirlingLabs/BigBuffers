using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Magic;

namespace BigBuffers
{
  [PublicAPI]
  public struct Placeholder
  {
#if DEBUG
    private readonly struct _t { }

    private static readonly ConcurrentDictionary<(BigBufferBuilder Buffer, ulong Offset), _t> Tracker
      = new();
#endif

    internal BigBufferBuilder Builder;
    internal readonly ulong Offset;

    public Placeholder(BigBufferBuilder builder, ulong offset)
    {
      Builder = builder;
      Offset = offset;
#if DEBUG
      if (!Tracker.TryAdd((Builder, Offset), default))
        throw new InvalidOperationException("Don't create multiple placeholders for the same data.");
#endif
    }

#if DEBUG
    public static Placeholder? GetPlaceholder(BigBufferBuilder bb, ulong offset)
      => Tracker.TryGetValue((bb, offset), out _)
        ? new(bb, offset)
        : default;


    public static bool IsPlaceholder(BigBufferBuilder bb, ulong offset)
      => Tracker.ContainsKey((bb, offset));
#endif

    private void Done()
    {
      if (Builder is null) return;
#if DEBUG
      Tracker.TryRemove((Builder, Offset), out _);
#endif
      Builder = null;
    }

    public void Fill(Array s, uint alignment = 0)
    {
      var e = s.GetEnumerator();
      var t = s.GetType().GetElementType();
      var elemSize = (ulong)SizeOf.Get(t);
      if (alignment == 0)
        alignment = (uint)Math.Min(sizeof(ulong), elemSize);

      if (!e.MoveNext())
      {
        if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
        Builder.Prep(sizeof(ulong),0);
        Builder.Put(BigBufferBuilder.PlaceholderOffset);
        Builder.StartVector(elemSize, 0);
        Builder.EndVector(alignment);
        return;
      }

      var setter = GetArraySet(t);

      if (setter is null)
        throw new NotImplementedException();

      setter.Invoke(this, new object[] { s });
    }

    private static readonly ConcurrentDictionary<Type, MethodInfo> ArraySetCache = new();
    private static MethodInfo GetArraySet(Type type)
      => ArraySetCache.GetOrAdd(type, t => {
        MethodInfo setter = null;
        foreach (var mi in typeof(Placeholder).GetMethods(
          BindingFlags.Public
          | BindingFlags.DeclaredOnly
          | BindingFlags.Instance))
        {
          if (!mi.IsGenericMethodDefinition)
            continue;
          var ps = mi.GetParameters();
#if NETSTANDARD2_0
          if (!ps[0].ParameterType.IsArray)
            continue;
#else
          if (!ps[0].ParameterType.IsSZArray)
            continue;
#endif
          setter = mi.MakeGenericMethod(t);
          break;
        }
        return setter;

      });

    public void Fill(string s)
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      var offset = Builder.WriteSharedString(s);
      Builder.Prep(sizeof(ulong), 0);
      Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
      Done();
    }

    public delegate Offset<TStruct>[] InlineStructs<TStruct>()
      where TStruct : struct, IBigBufferStruct;

    public void FillInline<TStruct>(InlineStructs<TStruct> f)
      where TStruct : struct, IBigBufferStruct
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      var elemSize = Unsafe.NullRef<TStruct>().ByteSize;
      var alignment = (uint)Math.Min(sizeof(ulong), elemSize);
      Builder.StartVector(elemSize, 0);
      var lengthOffset = Builder.Offset - 8;
      var l = f();
      Builder.ByteBuffer.Put(lengthOffset, (ulong)l.LongLength);
      var offset = Builder.EndVector(alignment);
      Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
      Done();
    }

    [DebuggerStepThrough]
    public void Fill<T>(Offset<T>[] s, uint alignment = 0)
      => Fill((ReadOnlyBigSpan<Offset<T>>)s, alignment);


    [DebuggerStepThrough]
    public void Fill(Offset[] s, uint alignment = 0)
      => Fill((ReadOnlyBigSpan<Offset>)s, alignment);


    public void Fill<T>(ReadOnlyBigSpan<Offset<T>> s, uint alignment = 0)
    {
      if (IfType<T>.IsAssignableTo<IBigBufferStruct>())
        throw new InvalidOperationException($"Use the {nameof(FillInline)} method for vectors of struct types instead.");
      if (IfType<T>.IsAssignableTo<IBigBufferTable>())
      {
        if (alignment == 0) alignment = sizeof(ulong);
        Builder.StartVector(sizeof(ulong), s.Length);
        for (nuint i = 0; i < s.Length; ++i)
          Builder.Put(s[i].Value - Builder.Offset);
        var offset = Builder.EndVector(alignment);
        Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
        Done();
      }
      else
        Fill<Offset<T>>(s, alignment);
    }


    public void Fill(ReadOnlyBigSpan<Offset> s, uint alignment = 0)
    {
      if (alignment == 0) alignment = sizeof(ulong);
      Builder.StartVector(sizeof(ulong), s.LongLength);
      for (nuint i = 0; i < s.Length; ++i)
        Builder.Put(s[i].Value - Builder.Offset);
      var offset = Builder.EndVector(alignment);
      Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
      Done();
    }


    [DebuggerStepThrough]
    public void Fill<T>(T[] s, uint alignment = 0)
      => Fill((ReadOnlyBigSpan<T>)s, alignment);

    public void Fill<T>(ReadOnlyBigSpan<T> s, uint alignment = 0)
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      var elemSize = (ulong)Unsafe.SizeOf<T>();
      if (alignment == 0) alignment = (uint)Math.Min(sizeof(ulong), elemSize);
      var length = s.Length;
      Builder.StartVector(elemSize, length);
      if (IfType<T>.Is<string>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        for (var i = (nuint)0; i < length; ++i)
        {
          Builder.Put(Builder.CreateString(out var p));
          placeholders[i] = p;
        }
        var offset = Builder.EndVector(alignment);
        Builder.ByteBuffer.Put(Offset, offset.Value - Offset);

        // fill placeholders
        Builder.Prep(s.Length * sizeof(ulong),0);
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Fill(s[i] as string);
      }
      else if (IfType<T>.Is<string[]>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        Builder.Prep(s.Length * sizeof(ulong),0);
        for (var i = (nuint)0; i < length; ++i)
        {
          Builder.Put(Builder.CreateVector(out var p));
          placeholders[i] = p;
        }
        var offset = Builder.EndVector(alignment);
        Builder.ByteBuffer.Put(Offset, offset.Value - Offset);

        // fill placeholders
        Builder.Prep(s.Length * sizeof(ulong),0);
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Fill(s[i] as string[], alignment);
      }
      else if (IfType<T>.IsAssignableTo<Array>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        Builder.Prep(s.Length * sizeof(ulong),0);
        for (var i = (nuint)0; i < length; ++i)
        {
          Builder.Put(Builder.CreateVector(out var p));
          placeholders[i] = p;
        }
        var offset = Builder.EndVector(alignment);
        Builder.ByteBuffer.Put(Offset, offset.Value - Offset);

        // fill placeholders
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Fill(s[i] as Array, alignment);
      }
      else if (IfType<T>.IsAssignableTo<IVectorOffset>())
        throw new NotImplementedException();
      else
      {
        // no placeholders needed
        Builder.Prep(s.AsBytes().Length,0);
        Builder.Put(s);
        var offset = Builder.EndVector(alignment);
        Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
      }
      Done();
    }


    internal void FillOffset<T>(Offset<T> offset)
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      Builder.Prep(sizeof(ulong),0);
      Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
      Done();
    }

    [Conditional("DEBUG")]
    public static void ValidateAllFilled(BigBufferBuilder bb)
    {
#if DEBUG
      var placeholders = Tracker.Keys
        .Where(k => k.Buffer == bb)
        .Select(k => k.Offset)
        .ToImmutableSortedSet();

      if (!placeholders.Any())
        return;

      throw new PlaceholdersUnfilledException(placeholders);
#else
      throw new NotImplementedException("This should not exist under release conditions.");
#endif
    }

    [Conditional("DEBUG")]
    public static void ValidateUnfilledCount(BigBufferBuilder bb, int count)
    {
#if DEBUG
      var placeholders = Tracker.Keys
        .Where(k => k.Buffer == bb)
        .Select(k => k.Offset)
        .ToImmutableSortedSet();

      if (placeholders.Count!=count)
        throw new PlaceholdersUnfilledException(placeholders, $"Expected {count}, but there were {placeholders.Count} unfilled placeholders.");
#else
      throw new NotImplementedException("This should not exist under release conditions.");
#endif
    }
  }

  public struct Placeholder<T>
  {
    internal Placeholder Internal;

    public Placeholder(BigBufferBuilder bb, ulong offset)
      => Internal = new(bb, offset);

    public void Fill(Offset<T> offset)
      => Internal.FillOffset(offset);
  }
}
