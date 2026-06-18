using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Entities;

public class OrganizationInviteEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    [ForeignKey(nameof(Organization))]
    public Guid OrganizationId { get; set; }

    public OrganizationEntity? Organization { get; set; }

    public string EmailLookupHash { get; set; } = string.Empty;

    public string EmailCiphertext { get; set; } = string.Empty;

    public OrganizationMemberRole Role { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public Guid InvitedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public OrganizationInviteStatus Status { get; set; }
}
