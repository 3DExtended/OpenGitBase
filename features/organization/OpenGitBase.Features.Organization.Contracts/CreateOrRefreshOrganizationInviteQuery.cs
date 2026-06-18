using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public class CreateOrRefreshOrganizationInviteQuery
    : IQuery<CreateOrRefreshOrganizationInviteResult, CreateOrRefreshOrganizationInviteQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public string Email { get; set; } = string.Empty;

    public OrganizationMemberRole Role { get; set; } = OrganizationMemberRole.Member;

    public Guid InvitedByUserId { get; set; }
}
