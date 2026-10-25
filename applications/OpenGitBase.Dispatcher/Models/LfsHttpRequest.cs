namespace OpenGitBase.Dispatcher.Models;

public sealed class LfsHttpRequest
{
    /// <summary><c>{owner}/{repository}</c> as in the git paths.</summary>
    public string RepositoryPath { get; init; } = string.Empty;

    public LfsRequestKind Kind { get; init; }

    /// <summary>The object of an upload, download or verify; null otherwise.</summary>
    public string? Oid { get; init; }
}
