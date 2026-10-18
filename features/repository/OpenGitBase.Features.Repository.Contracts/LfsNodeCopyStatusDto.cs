namespace OpenGitBase.Features.Repository.Contracts;

public sealed class LfsNodeCopyStatusDto
{
    public Guid StorageNodeId { get; init; }

    public LfsObjectCopyKind Kind { get; init; }

    public bool IsPrimary { get; init; }

    /// <summary>Recorded objects the node holds a copy of.</summary>
    public int Copies { get; init; }

    /// <summary>Recorded objects the node has no copy of yet.</summary>
    public int Missing { get; init; }
}
