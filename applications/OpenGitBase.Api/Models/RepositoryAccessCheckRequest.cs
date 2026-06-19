namespace OpenGitBase.Api.Models;

public class RepositoryAccessCheckRequest
{
    public string PublicKey { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string RepositoryPath { get; set; } = null!;
    public RepositoryOperation Operation { get; set; }

    public long PackSizeBytes { get; set; }

    public long MaxFileBytes { get; set; }
}
