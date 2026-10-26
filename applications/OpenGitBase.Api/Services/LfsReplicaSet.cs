namespace OpenGitBase.Api.Services;

/// <summary>
/// The healthy storage nodes an LFS object of a repository is copied to: the write primary, the plaintext
/// replicas on other nodes (the read replica) and the encrypted replicas.
/// </summary>
public sealed record LfsReplicaSet(
    Guid RepositoryId,
    StorageNodeEndpoint Primary,
    IReadOnlyList<StorageNodeEndpoint> PlaintextReplicas,
    IReadOnlyList<StorageNodeEndpoint> EncryptedReplicas
);
