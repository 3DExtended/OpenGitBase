using System.Text.Json.Serialization;

namespace OpenGitBase.Api.Models;

public sealed class LfsObjectError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
