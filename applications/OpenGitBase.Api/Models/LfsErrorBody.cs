using System.Text.Json.Serialization;

namespace OpenGitBase.Api.Models;

/// <summary>A Git LFS error response; the LFS client shows <see cref="Message"/> to the user.</summary>
public sealed class LfsErrorBody
{
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("request_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; init; }
}
