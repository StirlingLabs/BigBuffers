using System.Text.Json;

namespace BigBuffers.JsonParsing
{
  internal interface IJsonElementParser
  {
    void Parse(JsonElement element);
  }
}
