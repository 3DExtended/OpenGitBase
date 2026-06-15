using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Entities;

public class RepositoryEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    [ForeignKey(nameof(OwnerUser))]
    public Guid OwnerUserId { get; set; }

    public UserEntity? OwnerUser { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string PhysicalPath { get; set; } = string.Empty;

    public bool IsPrivate { get; set; } = false;

    public long StorageBytesUsed { get; set; }
}
