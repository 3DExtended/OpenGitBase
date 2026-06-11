using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.PublicGitSshKey.Contracts;

public class PublicGitSshKeyDto : ModelBase<PublicGitSshKeyId, Guid>
{
    public UserId OwnerUserId { get; set; } = default!;

    // User defined name for the SSH key, e.g. "My Laptop SSH Key"
    public string Name { get; set; } = string.Empty;

    public string PublicSSHKey { get; set; } = string.Empty;
}
