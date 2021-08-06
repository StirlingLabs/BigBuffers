using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BigBuffers.JsonParsing
{
  internal static class JsonUtilities
  {
    public static TEnum ResolveEnum<TEnum>(JsonElement element)
      where TEnum : unmanaged, Enum
    {
      TEnum result;
      switch (element.ValueKind)
      {
        case JsonValueKind.String:
          result = (TEnum)Enum.Parse(typeof(TEnum), element.GetString()!);
          break;
        case JsonValueKind.Number: {
          var v = element.GetDouble();
          if (v < 0)
          {
            var sv = (long)v;
            result = Unsafe.As<long, TEnum>(ref sv);
          }
          else
          {
            var uv = (ulong)v;
            result = Unsafe.As<ulong, TEnum>(ref uv);
          }
          break;
        }
        default: throw new NotImplementedException();
      }
      return result;
    }
  }
}
