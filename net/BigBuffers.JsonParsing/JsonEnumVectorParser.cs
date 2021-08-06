using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using JetBrains.Annotations;

namespace BigBuffers.JsonParsing
{
  [PublicAPI]
  internal sealed class JsonEnumVectorParser<TModel, TEnum> : IJsonVectorParser<TEnum>
    where TModel : struct, IBigBufferEntity
    where TEnum : System.Enum
  {
    private readonly JsonParser<TModel> _model;
    private Placeholder _placeholder;
    public JsonEnumVectorParser(JsonParser<TModel> model, Placeholder placeholder)
    {
      _model = model;
      _placeholder = placeholder;
    }

    public void Parse(JsonElement element)
      => _model.DeferredQueue.Enqueue(
        () => {

          var items = new TEnum[element.GetArrayLength()];

          var i = -1;
          foreach (var item in element.EnumerateArray())
          {
            if (item.ValueKind == JsonValueKind.String)
            {
              items[++i] = (TEnum)Enum.Parse(typeof(TEnum), item.GetString()!);
            }
            else if (item.ValueKind == JsonValueKind.Number)
            {
              var v = item.GetDouble();
              if (v < 0)
              {
                var sv = (long)v;
                items[++i] = Unsafe.As<long, TEnum>(ref sv);
              }
              else
              {
                var uv = (ulong)v;
                items[++i] = Unsafe.As<ulong, TEnum>(ref uv);
              }
            }
            else throw new NotImplementedException();
          }

          _placeholder.Fill(items);
        });
  }
}
