namespace OpenGitBase.Dispatcher.Models;

/// <summary>A client's batch request plus its credentials and LFS URL, for the API.</summary>
public sealed class LfsBatchForwardRequest
{
    public string AccessToken { get; init; } = string.Empty;

    public string RepositoryPath { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public IReadOnlyList<string>? Transfers { get; init; }

    public string? HashAlgo { get; init; }

    public IReadOnlyList<LfsObjectSpec> Objects { get; init; } = [];

    public string LfsBaseUrl { get; init; } = string.Empty;
}
