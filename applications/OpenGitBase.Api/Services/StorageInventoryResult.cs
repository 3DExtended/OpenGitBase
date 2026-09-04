namespace OpenGitBase.Api.Services;

/// <summary>
/// The repositories physically present on a storage node, as enumerated by the node itself:
/// plaintext bare repos under <c>/srv/git</c> and encrypted artifact sets under the artifact
/// root. The control plane cross-references this against its <c>RepositoryReplica</c> records to
/// detect storage/DB drift in both directions (on-disk copies with no record, records with no
/// on-disk data).
/// </summary>
public sealed record StorageInventoryResult(
    bool Success,
    int StatusCode,
    IReadOnlyList<Guid> PlaintextRepositories,
    IReadOnlyList<Guid> ArtifactRepositories,
    string? Error = null
)
{
    public static StorageInventoryResult Ok(
        IReadOnlyList<Guid> plaintextRepositories,
        IReadOnlyList<Guid> artifactRepositories
    ) => new(true, 200, plaintextRepositories, artifactRepositories);

    public static StorageInventoryResult Fail(int statusCode, string error) =>
        new(false, statusCode, [], [], error);
}
