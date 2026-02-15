using System.Text.Json.Serialization;
using Ready4Balfolk.Domain.Serialization;

namespace Ready4Balfolk.Domain.Models.Synonyms;

[JsonConverter(typeof(DanceSynonymJsonConverter))]
public sealed record DanceSynonym(string Name);
