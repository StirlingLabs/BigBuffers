using System.Text.Json;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
{
  [PublicAPI]
  internal sealed class JsonTableVectorParser<TModel, TVector> : IJsonVectorParser<TVector>
    where TModel : struct, IBigBufferEntity
    where TVector : struct, IBigBufferTable
  {
    private readonly JsonParser<TModel> _parser;
    private Placeholder _placeholder;
    public JsonTableVectorParser(JsonParser<TModel> parser, Placeholder placeholder)
    {
      _parser = parser;
      _placeholder = placeholder;
    }

    public void Parse(JsonElement element)
      => Runtime.Assert(_parser.DeferredActions.TryAdd(
        () => {

          var items = new Offset<TVector>[element.GetArrayLength()];

          var i = -1;
          foreach (var item in element.EnumerateArray())
          {
            var parser = new JsonParser<TVector>(_parser);
            items[++i] = parser.Parse(item);
          }

          _placeholder.Fill(items);
        }));
  }
}
