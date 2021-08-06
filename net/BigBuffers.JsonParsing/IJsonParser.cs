#nullable enable
using System;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BigBuffers.JsonParsing
{
  public interface IJsonParser
  {
    BigBufferBuilder Builder { get; }

    ConcurrentQueue<Action> DeferredQueue { get; }

    bool IsInline { get; }

    Type Type { get; }
    Type MetadataType { get; }

    ref readonly (string Name, bool Deprecated, ushort Offset, ushort Size, ushort Align, object Default)
      GetMetadata(ulong index);

    IBigBufferEntity Entity { get; }

#if DEBUG
    public IJsonParser? Parent { get; }
    public List<Expression> ExpressionsTaken { get; }
#endif
  }

  public interface IJsonParser<out T> : IJsonParser
    where T : struct, IBigBufferEntity
  {
    public T Parse(JsonElement element);

    new T Entity { get; }
  }
}
