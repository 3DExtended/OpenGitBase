namespace OpenGitBase.Dispatcher.Models;

public sealed class GitSmartHttpRequest
{
    public string RepositoryPath { get; set; } = string.Empty;

    public RepositoryOperation Operation { get; set; }

    public string GitSuffix { get; set; } = string.Empty;
}
