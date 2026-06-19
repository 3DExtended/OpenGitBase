namespace OpenGitBase.Dispatcher.Models;

public sealed class RepositoryAccessCheckRequest
{
    public string PublicKey { get; init; } = string.Empty;

    public string AccessToken { get; init; } = string.Empty;

    public required string RepositoryPath { get; init; }

    public RepositoryOperation Operation { get; init; }
}
