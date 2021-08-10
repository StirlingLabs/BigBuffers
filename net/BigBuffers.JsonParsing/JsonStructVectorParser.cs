using System.Text.Json;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
{
  [PublicAPI]
  internal sealed class JsonStructVectorParser<TModel, TVector> : IJsonVectorParser<TVector>
    where TModel : struct, IBigBufferEntity
    where TVector : struct, IBigBufferStruct
  {
    private readonly JsonParser<TModel> _parser;
    private Placeholder _placeholder;
    public JsonStructVectorParser(JsonParser<TModel> parser, Placeholder placeholder)
    {
      _parser = parser;
      _placeholder = placeholder;
    }

    public void Parse(JsonElement element)
      => Runtime.Assert(_parser.DeferredActions.TryAdd(() => {
        _placeholder.FillInline(() => {
          var items = new Offset<TVector>[element.GetArrayLength()];
          var builder = _parser.Builder;

          var i = -1;
          var model = new TVector();
          foreach (var item in element.EnumerateArray())
          {
            var parser = new JsonParser<TVector>(_parser) { Entity = new() };
            parser.Entity.Model = new(builder.ByteBuffer, builder.Offset);
            items[++i] = parser.Parse(item);
            builder.Offset += model.ByteSize;
            builder.Prep(model.Alignment, 0);
          }

          return items;
        });
      }));
  }
}
