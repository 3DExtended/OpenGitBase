using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// Relays an encrypted replication artifact from the primary storage node to a
/// target encrypted replica. Storage nodes hold per-node API tokens and cannot
/// authenticate to one another's internal HTTP endpoints directly, so the
/// control-plane API — which knows every node's token — performs the upload on
/// the primary's behalf.
/// </summary>
public sealed class RelayReplicationArtifactQuery
    : IQuery<RelayReplicationArtifactResult, RelayReplicationArtifactQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    /// <summary>The calling (primary) storage node, established by authentication.</summary>
    public StorageNodeId StorageNodeId { get; set; } = default!;

    /// <summary>The encrypted replica that should receive the artifact.</summary>
    public Guid TargetStorageNodeId { get; set; }

    public long Watermark { get; set; }

    public string ManifestJson { get; set; } = "{}";

    public byte[] BundlePayload { get; set; } = [];
}
