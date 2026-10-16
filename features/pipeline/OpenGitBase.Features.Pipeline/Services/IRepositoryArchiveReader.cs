namespace OpenGitBase.Features.Pipeline.Services;

/// <summary>
/// Builds the workspace a CI job starts from. Bare repositories live on the storage nodes, not
/// next to the API, so the host implements this over the storage content API.
/// </summary>
public interface IRepositoryArchiveReader
{
    /// <summary>
    /// Returns a tar.gz of the commit's files when <paramref name="gitDepth"/> is 0, else of a
    /// shallow clone checked out at the commit with its last <paramref name="gitDepth"/> commits.
    /// Throws <see cref="RepositoryFileUnavailableException"/> when storage cannot build it (no
    /// healthy node, commit not on that node, request failed).
    /// </summary>
    Task<byte[]> ReadWorkspaceArchiveAsync(
        Guid repositoryId,
        string physicalPath,
        string commitSha,
        int gitDepth,
        CancellationToken cancellationToken
    );
}
