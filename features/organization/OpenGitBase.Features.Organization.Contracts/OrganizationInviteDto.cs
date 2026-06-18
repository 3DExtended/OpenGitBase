using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Organization.Contracts;

public class OrganizationInviteDto : ModelBase<OrganizationInviteId, Guid>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public string Email { get; set; } = string.Empty;

    public OrganizationMemberRole Role { get; set; }

    public Guid InvitedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public OrganizationInviteStatus Status { get; set; }
}
