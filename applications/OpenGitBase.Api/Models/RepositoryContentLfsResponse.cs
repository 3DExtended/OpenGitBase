namespace OpenGitBase.Api.Models;

/// <summary>
/// The Git LFS object behind a pointer blob. <see cref="Available"/> says whether the object was uploaded to the
/// repository; the raw endpoint serves its content.
/// </summary>
public sealed class RepositoryContentLfsResponse
{
    public string Oid { get; init; } = string.Empty;

    public long Size { get; init; }

    public bool Available { get; init; }
}
