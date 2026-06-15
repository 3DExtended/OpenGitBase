using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.Entities;

public class OrganizationEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    [ForeignKey(nameof(OwnerUser))]
    public Guid OwnerUserId { get; set; }

    public UserEntity? OwnerUser { get; set; }
}
