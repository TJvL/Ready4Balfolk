using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ready4Balfolk.Domain.Stores.Settings;

/// <summary>Reads enums by name, and answers a value this build has never heard of with the first member.</summary>
/// <remarks>
/// <para>
/// The stock string converter throws, and one throw takes the whole settings file with it: a member
/// renamed between builds, or a typo in a file the user is invited to edit by hand, and the DJ opens
/// the app at the venue with every setting back to its factory value. One unreadable field costing
/// that field is the trade being made here. The nights are read with it for the same reason: a
/// status a later build adds would otherwise make a whole evening unreadable on an earlier one.
/// </para>
/// <para>
/// <c>Enum.TryParse</c> also accepts a number, and hands
/// back whatever that number casts to whether or not a member carries it. A "7" in a tag trust
/// list is the same mistake as a misspelt name, so it is answered the same way: a value with no
/// member would otherwise travel on into code that switches over the members and has no arm for it.
/// </para>
/// </remarks>
internal sealed class LenientEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(Converter<>).MakeGenericType(typeToConvert))!;

    private sealed class Converter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
            return Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && Enum.IsDefined(value) ? value : default;
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
