using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace BigBuffers
{
  public static class SizeOf<T>
  {
    public static readonly int Value = Unsafe.SizeOf<T>();
  }

  public static class SizeOf
  {
    private static readonly ConcurrentDictionary<Type, int> Sizes = new();

    public static int Get(Type type)
      => type.IsGenericTypeDefinition
        ? throw new InvalidOperationException("Can't get the size of an generic type definition, fill the generic type arguments first.")
        : Sizes.GetOrAdd(type, t => (int)typeof(SizeOf<>).MakeGenericType(t).InvokeMember("Value",
          BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField, null, null, null));
  }
}
