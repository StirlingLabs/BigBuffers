using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using StirlingLabs.Utilities;
using StirlingLabs.Utilities.Magic;

namespace BigBuffers
{
  public readonly struct Placeholder
  {
    private readonly BigBufferBuilder _bb;
    private readonly ulong _offset;
    public Placeholder(BigBufferBuilder bb, ulong offset)
    {
      _bb = bb;
      _offset = offset;
    }

    public void Set(Array s, uint alignment = 0)
    {
      var e = s.GetEnumerator();
      var t = s.GetType().GetElementType();
      var elemSize = (ulong)SizeOf.Get(t);
      if (alignment == 0)
        alignment = (uint)Math.Min(8u, elemSize);

      if (!e.MoveNext())
      {
        _bb.Put(0uL);
        _bb.StartVector(elemSize, 0);
        _bb.EndVector(alignment);
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

    public void Set(string s)
    {
      var offset = _bb.CreateString(s);
      _bb.Prep(8, 0);
      _bb.ByteBuffer.Put(_offset, offset.Value - _offset);
    }

    [DebuggerStepThrough]
    public void Set<T>(T[] s, uint alignment = 0)
      => Set((ReadOnlyBigSpan<T>)s, alignment);

    public void Set<T>(ReadOnlyBigSpan<T> s, uint alignment = 0)
    {
      var elemSize = (ulong)Unsafe.SizeOf<T>();
      if (alignment == 0) alignment = (uint)Math.Min(8u, elemSize);
      var length = s.LongLength;
      _bb.StartVector(elemSize, length);
      if (IfType<T>.Is<string>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        for (var i = (nuint)0; i < length; ++i)
        {
          _bb.Put(_bb.CreateString(out var p));
          placeholders[i] = p;
        }
        var offset = _bb.EndVector(alignment);
        _bb.ByteBuffer.Put(_offset, offset.Value - _offset);

        // fill placeholders
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Set(s[i] as string);
      }
      else if (IfType<T>.Is<string[]>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        for (var i = (nuint)0; i < length; ++i)
        {
          _bb.Put(_bb.CreateVector(out var p));
          placeholders[i] = p;
        }
        var offset = _bb.EndVector(alignment);
        _bb.ByteBuffer.Put(_offset, offset.Value - _offset);

        // fill placeholders
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Set(s[i] as string[], alignment);
      }
      else if (IfType<T>.IsAssignableTo<Array>())
      {
        var placeholders = new Placeholder[length];

        // create placeholders
        for (var i = (nuint)0; i < length; ++i)
        {
          _bb.Put(_bb.CreateVector(out var p));
          placeholders[i] = p;
        }
        var offset = _bb.EndVector(alignment);
        _bb.ByteBuffer.Put(_offset, offset.Value - _offset);

        // fill placeholders
        for (var i = (nuint)0; i < length; ++i)
          placeholders[i].Set(s[i] as Array, alignment);
      }
      else if (IfType<T>.IsAssignableTo<IVectorOffset>())
        throw new NotImplementedException();
      else
      {
        // no placeholders needed
        _bb.Put(s);
        var offset = _bb.EndVector(alignment);
        _bb.ByteBuffer.Put(_offset, offset.Value - _offset);
      }
    }
  }
}
