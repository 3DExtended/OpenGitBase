namespace OpenGitBase.Dispatcher.Models;

public sealed class LfsTransferCheckForwardRequest
{
    public string Token { get; init; } = string.Empty;

    public string Oid { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;
}
