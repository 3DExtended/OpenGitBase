namespace OpenGitBase.Api.Services;

/// <summary>
/// The copies of an LFS object that verify did not wait for: the encrypted uploads still running (or failed)
/// when quorum was reached, and the plaintext replicas that have not been copied to yet.
/// </summary>
public sealed record LfsPendingCopies(
    LfsReplicaSet Replicas,
    string Oid,
    long Size,
    LfsCipherContext Cipher,
    IReadOnlyList<Task<LfsCopyAttempt>> EncryptedUploads,
    IReadOnlySet<Guid> RecordedNodeIds
);
