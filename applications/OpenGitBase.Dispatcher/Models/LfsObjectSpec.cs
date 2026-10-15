using System.Text.Json.Serialization;

namespace OpenGitBase.Dispatcher.Models;

public sealed class LfsObjectSpec
{
    [JsonPropertyName("oid")]
    public string Oid { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
