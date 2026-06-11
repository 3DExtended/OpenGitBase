using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Entities;

public class PublicGitSshKeyEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    [ForeignKey(nameof(OwnerUser))]
    public Guid OwnerUserId { get; set; }

    public UserEntity? OwnerUser { get; set; }

    // User defined name for the SSH key, e.g. "My Laptop SSH Key"
    public string Name { get; set; } = string.Empty;

    public string PublicSSHKey { get; set; } = string.Empty;
}
