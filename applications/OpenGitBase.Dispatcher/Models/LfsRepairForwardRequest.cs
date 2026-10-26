namespace OpenGitBase.Dispatcher.Models;

/// <summary>Tells the API that no storage target held an object a download asked for.</summary>
public sealed class LfsRepairForwardRequest
{
    public string Token { get; init; } = string.Empty;

    public string Oid { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;
}
