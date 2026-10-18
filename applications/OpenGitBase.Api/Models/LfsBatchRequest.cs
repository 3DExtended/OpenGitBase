namespace OpenGitBase.Api.Models;

/// <summary>A Git LFS batch request as the dispatcher forwards it, with the client's credentials.</summary>
public sealed class LfsBatchRequest
{
    /// <summary>Personal access token from the client's Basic auth header; empty when it sent none.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary><c>{owner}/{repository}</c> from the request path.</summary>
    public string RepositoryPath { get; set; } = string.Empty;

    /// <summary><c>upload</c> or <c>download</c>.</summary>
    public string Operation { get; set; } = string.Empty;

    public IReadOnlyList<string>? Transfers { get; set; }

    public string? HashAlgo { get; set; }

    public IReadOnlyList<LfsBatchObjectRequest> Objects { get; set; } = [];

    /// <summary>The LFS endpoint as the client reached it, such as <c>https://host/owner/repo.git/info/lfs</c>.</summary>
    public string? LfsBaseUrl { get; set; }
}
