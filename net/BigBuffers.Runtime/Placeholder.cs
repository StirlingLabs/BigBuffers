using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using StirlingLabs.Utilities;
using static BigBuffers.Debug;

namespace BigBuffers
{
  [PublicAPI]
  public struct Placeholder
  {
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    private readonly struct EmptyType { }

    private static readonly ConcurrentDictionary<(BigBufferBuilder Buffer, ulong Offset), EmptyType> Tracker
      = new();

    private static bool _enableValidation
#if DEBUG
      = true;
#else
      = false;
#endif

    public static bool EnableValidation
    {
      get => _enableValidation;
      set {
        if (value == _enableValidation) return;

        Tracker.Clear();
        _enableValidation = value;
      }
    }

    internal BigBufferBuilder Builder;
    internal readonly ulong Offset;

    public Placeholder(BigBufferBuilder builder, ulong offset)
    {
      IsAtLeastMinimumAlignment(offset, sizeof(ulong));
      Debug.Assert(builder != null);
      Builder = builder;
      Offset = offset;

      if (!EnableValidation)
        return;

      if (!Tracker.TryAdd((Builder, Offset), default))
        throw new InvalidOperationException("Don't create multiple placeholders for the same data.");
    }


    public bool IsFilled
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => Builder is null;
    }

    public bool IsUnfilled
    {
      [MethodImpl(MethodImplOptions.AggressiveInlining)]
      get => Builder is not null;
    }

    public static Placeholder? GetPlaceholder(BigBufferBuilder bb, ulong offset)
    {
      if (!EnableValidation)
        throw new InvalidOperationException("EnableValidation must be set to true in order to use validation.");

      return Tracker.TryGetValue((bb, offset), out _)
        ? new(bb, offset)
        : default;
    }


    public static bool IsPlaceholder(BigBufferBuilder bb, ulong offset)
    {
      if (!EnableValidation)
        throw new InvalidOperationException("EnableValidation must be set to true in order to use validation.");

      return Tracker.ContainsKey((bb, offset));
    }

    private void Done()
    {
      if (Builder is null) return;
      if (EnableValidation)
        Tracker.TryRemove((Builder, Offset), out _);
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
        Builder.Prep(sizeof(ulong), 0);
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

    public delegate Offset<TStruct>[] FillInlineStructs<TStruct>()
      where TStruct : struct, IBigBufferStruct;

    public void FillInline<TStruct>([InstantHandle] FillInlineStructs<TStruct> f)
      where TStruct : struct, IBigBufferStruct
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      var elemSize = Unsafe.NullRef<TStruct>().ByteSize;
      var alignment = (uint)Math.Min(sizeof(ulong), elemSize);
      Builder.StartVector(elemSize, 0);
      var lengthOffset = Builder.Offset - sizeof(ulong);
      var l = f();
      var itemCount = (ulong)l.LongLength;
      Debug.Assert(Builder.Offset == lengthOffset + sizeof(ulong) + elemSize * itemCount);
      Builder.ByteBuffer.Put(lengthOffset, itemCount);
      var offset = Builder.EndVector(alignment);
      Builder.ByteBuffer.Put(Offset, offset.Value - Offset);
      Done();
    }


    public void FillVector([InstantHandle] Func<ulong> filler)
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      Builder.StartVector(sizeof(ulong), 0);
      var lengthOffset = Builder.Offset - sizeof(ulong);
      var itemCount = filler();
      Builder.ByteBuffer.Put(lengthOffset, itemCount);
      var vectorOffset = Builder.EndVector();
      var offsetValue = vectorOffset.Value - Offset;
      IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
      Builder.ByteBuffer.Put(Offset, offsetValue);
      Done();
    }

    [DebuggerStepThrough]
    public void Fill<T>(Offset<T>[] s, uint alignment = 0)
      where T : struct, IBigBufferEntity
      => throw new InvalidOperationException(
        $"Use the Fill method that has {nameof(Func<Offset<T>[]>)} as the first parameter type for vectors of struct offset types instead.");

    [DebuggerStepThrough]
    public void Fill(StringOffset[] s, uint alignment = 0)
    {
      var length = (ulong)s.LongLength;
      Builder.StartVector(sizeof(ulong), length);
      for (var i = (nuint)0; i < length; ++i)
      {
        var offset = Offset - s[i].Value;
        IsAtLeastMinimumAlignment(offset, sizeof(ulong));
        Builder.Put(offset);
      }
      var vectorOffset = Builder.EndVector(alignment);
      var offsetValue = vectorOffset.Value - Offset;
      IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
      Builder.ByteBuffer.Put(Offset, offsetValue);
    }


    [DebuggerStepThrough]
    public void Fill(Offset[] s, uint alignment = 0)
      => throw new InvalidOperationException(
        $"Use the Fill method that has {nameof(Func<Offset>)} as the first parameter type for vectors of offsets instead.");
    //=> Fill((ReadOnlyBigSpan<Offset>)s, alignment);


    public void Fill<T>(ReadOnlyBigSpan<Offset<T>> s, uint alignment = 0)
      where T : struct, IBigBufferEntity
    {
      if (Type<T>.IsAssignableTo<IBigBufferStruct>())
        throw new InvalidOperationException($"Use the {nameof(FillInline)} method for vectors of struct types instead.");
      if (Type<T>.IsAssignableTo<IBigBufferTable>())
      {
        if (alignment == 0) alignment = sizeof(ulong);
        Builder.StartVector(sizeof(ulong), s.Length);
        for (nuint i = 0; i < s.Length; ++i)
        {
          var offset = s[i].Value - Builder.Offset;
          IsAtLeastMinimumAlignment(offset, sizeof(ulong));
          Builder.Put(offset);
        }
        var vectorOffset = Builder.EndVector(alignment);
        var offsetValue = vectorOffset.Value - Offset;
        IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
        Builder.ByteBuffer.Put(Offset, offsetValue);
        Done();
      }
      else
        throw new InvalidOperationException(
          $"Use the Fill method that has {nameof(Func<Offset<T>[]>)} as the first parameter type for vectors of struct offset types instead.");
    }


    public void Fill(ReadOnlyBigSpan<Offset> s, uint alignment = 0)
    {
      if (alignment == 0) alignment = sizeof(ulong);
      Builder.StartVector(sizeof(ulong), s.LongLength);
      for (nuint i = 0; i < s.Length; ++i)
      {
        var offset = s[i].Value - Builder.Offset;
        IsAtLeastMinimumAlignment(offset, sizeof(ulong));
        Builder.Put(offset);
      }
      var vectorOffset = Builder.EndVector(alignment);
      var offsetValue = vectorOffset.Value - Offset;
      IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
      Builder.ByteBuffer.Put(Offset, offsetValue);
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
      if (Type<T>.Is<string>())
      {
        var placeholders = new Placeholder[length];

        Builder.StartVector(elemSize, length);
        // create placeholders
        for (var i = (nuint)0; i < length; ++i)
        {
          Builder.Put(Builder.MarkStringPlaceholder(out var p));
          placeholders[i] = p;
        }
        var offset = Builder.EndVector(alignment);
        Builder.ByteBuffer.Put(Offset, offset.Value - Offset);

        // fill placeholders
        Builder.Prep(length * sizeof(ulong), 0);
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Fill(s[i] as string);
      }
      else if (Type<T>.Is<string[]>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        Builder.StartVector(elemSize, length);
        for (var i = (nuint)0; i < length; ++i)
        {
          Builder.Put(Builder.MarkVectorPlaceholder(out var p));
          placeholders[i] = p;
        }
        var offset = Builder.EndVector(alignment);
        var offsetValue = offset.Value - Offset;
        IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
        Builder.ByteBuffer.Put(Offset, offsetValue);

        // fill placeholders
        Builder.Prep(length * sizeof(ulong), 0);
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Fill(s[i] as string[], alignment);
      }
      else if (Type<T>.IsAssignableTo<Array>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        Builder.StartVector(elemSize, length);
        for (var i = (nuint)0; i < length; ++i)
        {
          Builder.Put(Builder.MarkVectorPlaceholder(out var p));
          placeholders[i] = p;
        }
        var offset = Builder.EndVector(alignment);
        var offsetValue = offset.Value - Offset;
        IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
        Builder.ByteBuffer.Put(Offset, offsetValue);

        // fill placeholders
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Fill(s[i] as Array, alignment);
      }
      else if (Type<T>.IsAssignableTo<StringOffset>())
      {
        Builder.StartVector(elemSize, length);
        var sos = s.CastAs<StringOffset>();
        for (var i = (nuint)0; i < length; ++i)
        {
          var so = sos[i];
          Builder.Put(Offset - so.Value);
        }
        var offset = Builder.EndVector(alignment);
        var offsetValue = offset.Value - Offset;
        IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
        Builder.ByteBuffer.Put(Offset, offsetValue);
      }
      else if (Type<T>.IsAssignableTo<IVectorOffset>())
      {
        throw new NotImplementedException(); // ???
      }
      else
      {
        // no placeholders needed
        Builder.StartVector(elemSize, length);
        Builder.Put(s);
        var offset = Builder.EndVector(alignment);
        var offsetValue = offset.Value - Offset;
        IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
        Builder.ByteBuffer.Put(Offset, offsetValue);
      }
      Done();
    }

    internal void FillOffset<T>(Offset<T> offset)
      where T : struct, IBigBufferEntity
    {
      if (Builder is null) throw new InvalidOperationException("Placeholder has already been filled.");
      var offsetValue = offset.Value - Offset;
      IsAtLeastMinimumAlignment(offsetValue, sizeof(ulong));
      IsAtLeastMinimumAlignment(Offset, sizeof(ulong));
      Builder.ByteBuffer.Put(Offset, offsetValue);
      Done();
    }

    internal void FillOffsetValue(ulong value)
    {
      IsAtLeastMinimumAlignment(value, sizeof(ulong));
      FillValue(value);
    }


    internal void FillValue(ulong value)
    {
      IsAtLeastMinimumAlignment(Offset, sizeof(ulong));
      Builder.ByteBuffer.Put(Offset, value);
      Done();
    }

    public static void ValidateAllFilled(BigBufferBuilder bb)
    {
      if (!EnableValidation)
        throw new InvalidOperationException("EnableValidation must be set to true in order to use validation.");

      var placeholders = Tracker.Keys
        .Where(k => k.Buffer == bb)
        .Select(k => k.Offset)
        .ToImmutableSortedSet();

      if (!placeholders.Any())
        return;

      throw new PlaceholdersUnfilledException(placeholders);
    }

    public static void GetUnfilledCount(BigBufferBuilder bb, out int count)
    {
      if (!EnableValidation)
        throw new InvalidOperationException("EnableValidation must be set to true in order to use validation.");

      count = Tracker.Keys
        .Count(k => k.Buffer == bb);
    }

    public static void ValidateUnfilledCount(BigBufferBuilder bb, int count)
    {
      if (!EnableValidation)
        throw new InvalidOperationException("EnableValidation must be set to true in order to use validation.");

      var placeholders = Tracker.Keys
        .Where(k => k.Buffer == bb)
        .Select(k => k.Offset)
        .ToImmutableSortedSet();

      if (placeholders.Count != count)
        throw new PlaceholdersUnfilledException(placeholders, $"Expected {count}, but there were {placeholders.Count} unfilled placeholders.");
    }
  }

  public struct Placeholder<T>
    where T : struct, IBigBufferEntity
  {
    internal Placeholder Internal;

    public Placeholder(BigBufferBuilder bb, ulong offset)
      => Internal = new(bb, offset);

    public void Fill(Offset<T> offset)
      => Internal.FillOffset(offset);
  }
}
