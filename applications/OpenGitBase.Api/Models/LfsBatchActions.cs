using System.Text.Json.Serialization;

namespace OpenGitBase.Api.Models;

public sealed class LfsBatchActions
{
    [JsonPropertyName("download")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LfsBatchAction? Download { get; init; }

    [JsonPropertyName("upload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LfsBatchAction? Upload { get; init; }

    [JsonPropertyName("verify")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LfsBatchAction? Verify { get; init; }
}
