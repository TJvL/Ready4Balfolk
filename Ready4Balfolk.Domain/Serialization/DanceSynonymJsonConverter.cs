using System.Text.Json;
using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Models.Synonyms;

namespace Ready4Balfolk.Domain.Serialization;

public sealed class DanceSynonymJsonConverter : JsonConverter<DanceSynonym>
{
    public override DanceSynonym Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString()!);

    public override void Write(Utf8JsonWriter writer, DanceSynonym value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Name);
}
