namespace OpenGitBase.Api.Models;

public sealed class QuorumReplicateRepositoryRequest
{
    public long AppliedWatermark { get; init; }

    public IReadOnlyList<Guid> ConfirmedEncryptedNodeIds { get; init; } = [];
}
