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
        var parser = new JsonParser<T>(_parser);
        _placeholder.Fill(parser.Parse(element));
      });
  }
}
