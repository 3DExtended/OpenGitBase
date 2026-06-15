using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Organization.Entities;

public class OrganizationMemberEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    [ForeignKey(nameof(Organization))]
    public Guid OrganizationId { get; set; }

    public OrganizationEntity? Organization { get; set; }

    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }

    public OrganizationMemberRole Role { get; set; }
}
