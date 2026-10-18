namespace OpenGitBase.Features.Repository.Contracts;

public sealed class LfsObjectCopySpec
{
    public Guid StorageNodeId { get; init; }

    public LfsObjectCopyKind Kind { get; init; }
}
