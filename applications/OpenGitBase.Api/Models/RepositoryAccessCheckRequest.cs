namespace OpenGitBase.Api.Models;

public class RepositoryAccessCheckRequest
{
    public string PublicKey { get; set; } = null!;
    public string RepositoryPath { get; set; } = null!;
    public RepositoryOperation Operation { get; set; }
}
