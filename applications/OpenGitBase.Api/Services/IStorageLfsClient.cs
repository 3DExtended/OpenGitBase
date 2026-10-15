using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Git LFS object operations on a storage node's internal HTTP API. <see cref="LfsObjectCopyKind.Plaintext"/>
/// addresses the object itself (primary, read replica), <see cref="LfsObjectCopyKind.Encrypted"/> its encrypted
/// copy (encrypted replica). Bodies are streamed both ways.
/// </summary>
public interface IStorageLfsClient
{
    /// <summary>The stored size of the copy on the node, or null when the node has none.</summary>
    Task<long?> GetObjectSizeAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind,
        CancellationToken cancellationToken
    );

    /// <summary>The copy's bytes as a stream the caller disposes, or null when the node has none.</summary>
    Task<Stream?> OpenObjectAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// The repository's LFS copies on the node's disk, with the kind the node's role holds. Throws when the node
    /// cannot answer, so a caller never mistakes an unreachable node for an empty one.
    /// </summary>
    Task<LfsNodeInventory> GetInventoryAsync(StorageNodeEndpoint node, Guid repositoryId, CancellationToken cancellationToken);

    /// <summary>Stores <paramref name="content"/> (which must report its length) as the node's copy.</summary>
    Task PutObjectAsync(
        StorageNodeEndpoint node,
        Guid repositoryId,
        string oid,
        LfsObjectCopyKind kind,
        HttpContent content,
        CancellationToken cancellationToken
    );
}
