using System.Text.Json;
using OpenGitBase.Cli.Api;

namespace OpenGitBase.Cli.Tests;

public sealed class FlexibleGuidJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new FlexibleGuidJsonConverter() },
    };

    [Fact]
    public void Deserializes_plain_guid_string()
    {
        const string json = """{"id":"11111111-1111-1111-1111-111111111111"}""";
        var model = JsonSerializer.Deserialize<IdHolder>(json, Options);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), model!.Id);
    }

    [Fact]
    public void Deserializes_identifier_object_wrapper()
    {
        const string json = """{"id":{"value":"22222222-2222-2222-2222-222222222222"}}""";
        var model = JsonSerializer.Deserialize<IdHolder>(json, Options);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), model!.Id);
    }

    private sealed class IdHolder
    {
        public Guid Id { get; set; }
    }
}
