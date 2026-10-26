namespace OpenGitBase.Dispatcher.Models;

public sealed class LfsVerifyForwardRequest
{
    public string Token { get; init; } = string.Empty;

    public string Oid { get; init; } = string.Empty;

    public long Size { get; init; }

    public string RepositoryPath { get; init; } = string.Empty;
}
