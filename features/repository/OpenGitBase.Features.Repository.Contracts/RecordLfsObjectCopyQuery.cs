using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// Notes that a storage node now holds a copy of a recorded LFS object. Bookkeeping for routing and dashboards;
/// None when the object is not recorded.
/// </summary>
public sealed class RecordLfsObjectCopyQuery : IQuery<RecordLfsObjectResult, RecordLfsObjectCopyQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public string Oid { get; set; } = string.Empty;

    public Guid StorageNodeId { get; set; }

    public LfsObjectCopyKind Kind { get; set; }
}
