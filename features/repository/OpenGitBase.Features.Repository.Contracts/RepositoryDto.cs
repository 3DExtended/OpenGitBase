using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository.Contracts;

public class RepositoryDto : ModelBase<RepositoryId, Guid>
{
    public string Name { get; set; } = string.Empty;

    public UserId OwnerUserId { get; set; } = default!;

    public string Slug { get; set; } = string.Empty;

    public string PhysicalPath { get; set; } = string.Empty;

    public StorageNodeId? StorageNodeId { get; set; }

    public bool IsPrivate { get; set; } = false;

    public long StorageBytesUsed { get; set; }

    public string OwnerKind { get; set; } = "user";

    public string OwnerSlug { get; set; } = string.Empty;

    public string? DefaultBranchName { get; set; }

    public PlacementPolicy? PlacementPolicy { get; set; }
}
