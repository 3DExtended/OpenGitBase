namespace OpenGitBase.Api.Models;

public sealed class LfsBatchObjectRequest
{
    public string Oid { get; set; } = string.Empty;

    public long Size { get; set; }
}
