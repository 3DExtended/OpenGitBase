namespace OpenGitBase.Api.Models;

public sealed class QuorumReplicateRepositoryRequest
{
    public long AppliedWatermark { get; init; }

    /// <summary>
    /// Legacy hook flow: the encrypted replicas the hook already stored the artifact on (via
    /// /replicate-artifact). Ignored when <see cref="CreateArtifact"/> is set.
    /// </summary>
    public IReadOnlyList<Guid> ConfirmedEncryptedNodeIds { get; init; } = [];

    /// <summary>
    /// When true the API builds the encrypted artifact on the primary and uploads it to the
    /// encrypted replicas itself, so the hook sends no key, bundle or artifact.
    /// </summary>
    public bool CreateArtifact { get; init; }
}
