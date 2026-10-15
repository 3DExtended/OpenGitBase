namespace OpenGitBase.Api.Models;

/// <summary>The dispatcher asks whether an object transfer may start, before it moves a byte.</summary>
public sealed class LfsTransferCheckRequest
{
    /// <summary>The LFS action token from the transfer request's Authorization header.</summary>
    public string Token { get; set; } = string.Empty;

    public string Oid { get; set; } = string.Empty;

    /// <summary><c>upload</c> or <c>download</c>.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary><c>{owner}/{repository}</c> from the request path.</summary>
    public string RepositoryPath { get; set; } = string.Empty;
}
