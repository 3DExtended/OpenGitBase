using System.Text.Json.Serialization;

namespace OpenGitBase.Api.Models;

public sealed class LfsBatchAction
{
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    [JsonPropertyName("header")]
    public IReadOnlyDictionary<string, string> Header { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }
}
