using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGitBase.Cli.Api;

/// <summary>
/// Deserializes API identifiers that may be a plain GUID string or an object wrapper
/// such as <c>{ "value": "..." }</c> from <see cref="OpenGitBase.Cqrs.EfCore.Identifier{T,TSelf}"/>.
/// </summary>
public sealed class FlexibleGuidJsonConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => Guid.Parse(reader.GetString()!),
            JsonTokenType.StartObject => ReadFromObject(ref reader),
            _ => throw new JsonException($"Unexpected token {reader.TokenType} when parsing Guid."),
        };
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);

    private static Guid ReadFromObject(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        foreach (var property in root.EnumerateObject())
        {
            if (property.NameEquals("value") || property.NameEquals("Value"))
            {
                return property.Value.GetGuid();
            }
        }

        throw new JsonException("Cannot parse Guid from identifier object.");
    }
}
