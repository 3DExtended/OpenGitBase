namespace OpenGitBase.Features.Organization.Contracts;

public class OrganizationInvitePublicDto
{
    public OrganizationInviteId InviteId { get; set; } = default!;

    public OrganizationId OrganizationId { get; set; } = default!;

    public string OrganizationName { get; set; } = string.Empty;

    public string OrganizationSlug { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public OrganizationMemberRole Role { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public OrganizationInviteStatus Status { get; set; }
}
