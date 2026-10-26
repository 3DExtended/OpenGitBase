namespace OpenGitBase.Api.Models;

/// <summary>A Git LFS verify call (<c>{oid, size}</c>) as the dispatcher forwards it.</summary>
public sealed class LfsVerifyRequest
{
    public string Token { get; set; } = string.Empty;

    public string Oid { get; set; } = string.Empty;

    public long Size { get; set; }

    public string RepositoryPath { get; set; } = string.Empty;
}
