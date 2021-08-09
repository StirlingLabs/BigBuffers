using System.Text.Json;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
{
  [PublicAPI]
  internal sealed class JsonDeferredModelParser<TModel, T> : IJsonElementParser
    where TModel : struct, IBigBufferEntity
    where T : struct, IBigBufferEntity
  {
    private readonly JsonParser<TModel> _parser;
    private Placeholder<T> _placeholder;
    public JsonDeferredModelParser(JsonParser<TModel> parser, Placeholder<T> placeholder)
    {
      _parser = parser;
      _placeholder = placeholder;
    }

    public void Parse(JsonElement element)
      => _parser.DeferredQueue.Enqueue(() => {
        var builder = _parser.Builder;
        builder.Prep(ByteBuffer.AlignOf<T>(), 0);
        var parser = new JsonParser<T>(_parser);
        var entity = parser.Parse(element);
        Debug.Assert(builder.ByteBuffer.Buffer is not null);
        _placeholder.Fill(entity);
      });
  }
}
