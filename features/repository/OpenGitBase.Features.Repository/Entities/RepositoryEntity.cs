using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Repository.Entities;

public class RepositoryEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string PhysicalPath { get; set; } = string.Empty;

    public Guid? StorageNodeId { get; set; }

    public bool IsPrivate { get; set; } = false;

    public long StorageBytesUsed { get; set; }
}
