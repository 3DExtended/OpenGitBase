namespace OpenGitBase.Api.Models;

public sealed class RepositoryKeyResponse
{
    public string KeyBase64 { get; init; } = string.Empty;

    public int KeyVersion { get; init; }
}
