namespace OpenGitBase.Features.Pipeline.Services;

/// <summary>
/// Reads a file from a repository at a given commit. Bare repositories live on the storage
/// nodes, not next to the API, so the host implements this over the storage content API.
/// </summary>
public interface IRepositoryFileReader
{
    /// <summary>
    /// Returns the file's text, or <c>null</c> when the commit has no file at <paramref name="path"/>.
    /// Throws <see cref="RepositoryFileUnavailableException"/> when storage cannot answer yet
    /// (no healthy node, commit not on that node, request failed), so callers retry instead of
    /// mistaking an outage for a missing file.
    /// </summary>
    Task<string?> ReadTextAsync(
        Guid repositoryId,
        string physicalPath,
        string commitSha,
        string path,
        CancellationToken cancellationToken
    );
}
