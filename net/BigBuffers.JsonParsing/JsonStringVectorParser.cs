using System.Text.Json;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
{
  [PublicAPI]
  internal sealed class JsonStringVectorParser<TModel> : IJsonVectorParser<string>
    where TModel : struct, IBigBufferEntity
  {
    private readonly JsonParser<TModel> _parser;
    private Placeholder _placeholder;
    public JsonStringVectorParser(JsonParser<TModel> parser, Placeholder placeholder)
    {
      _parser = parser;
      _placeholder = placeholder;
    }

    public void Parse(JsonElement element)
      => _parser.DeferredQueue.Enqueue(
        () => {

          var items = new StringOffset[element.GetArrayLength()];

          var i = -1;
          var bb = _placeholder.Builder;
          foreach (var item in element.EnumerateArray())
            items[++i] = bb.WriteSharedString(item.GetString());

          _placeholder.FillVector(() => {
            var length = (ulong)items.LongLength;
            for (var index = 0uL; index < length; index++)
              bb.WriteOffset(items[index]);
            return length;
          });
        });
  }
}
