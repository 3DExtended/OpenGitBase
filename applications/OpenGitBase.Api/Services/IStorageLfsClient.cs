namespace OpenGitBase.Api.Services;

/// <summary>Git LFS object operations on a storage node's internal HTTP API.</summary>
public interface IStorageLfsClient
{
    /// <summary>The stored size of the object on the node, or null when the node has no copy.</summary>
    Task<long?> GetObjectSizeAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        CancellationToken cancellationToken
    );
}
