namespace OpenGitBase.Api.Models;

/// <summary>
/// The dispatcher found no storage target holding an LFS object it was asked to download. The download's action
/// token proves the caller may read the object.
/// </summary>
public sealed class LfsRepairRequest
{
    /// <summary>The LFS action token from the download request's Authorization header.</summary>
    public string Token { get; set; } = string.Empty;

    public string Oid { get; set; } = string.Empty;

    /// <summary><c>{owner}/{repository}</c> from the request path.</summary>
    public string RepositoryPath { get; set; } = string.Empty;
}
