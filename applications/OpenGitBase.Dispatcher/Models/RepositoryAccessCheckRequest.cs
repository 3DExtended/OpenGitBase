namespace OpenGitBase.Dispatcher.Models;

public sealed class RepositoryAccessCheckRequest
{
    public required string PublicKey { get; init; }
    public required string RepositoryPath { get; init; }
    public RepositoryOperation Operation { get; init; }
}
