using System.Text.Json.Serialization;

namespace OpenGitBase.Api.Models;

/// <summary>The Git LFS batch response, serialized exactly as the LFS client expects it.</summary>
public sealed class LfsBatchResponseBody
{
    [JsonPropertyName("transfer")]
    public string Transfer { get; init; } = "basic";

    [JsonPropertyName("objects")]
    public IReadOnlyList<LfsBatchObjectResponse> Objects { get; init; } = [];

    [JsonPropertyName("hash_algo")]
    public string HashAlgo { get; init; } = "sha256";
}
