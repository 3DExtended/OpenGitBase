using System.Text.Json.Serialization;

namespace OpenGitBase.Dispatcher.Models;

/// <summary>The batch request body as the git-lfs client sends it.</summary>
public sealed class LfsClientBatchRequest
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("transfers")]
    public List<string>? Transfers { get; set; }

    [JsonPropertyName("objects")]
    public List<LfsObjectSpec> Objects { get; set; } = [];

    [JsonPropertyName("hash_algo")]
    public string? HashAlgo { get; set; }
}
