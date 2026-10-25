using System.Text.Json.Serialization;

namespace OpenGitBase.Api.Models;

public sealed class LfsBatchObjectResponse
{
    [JsonPropertyName("oid")]
    public string Oid { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>The actions carry their own token, so the client must not add its credentials to them.</summary>
    [JsonPropertyName("authenticated")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Authenticated { get; init; }

    [JsonPropertyName("actions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LfsBatchActions? Actions { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LfsObjectError? Error { get; init; }
}
