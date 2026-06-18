using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public class ResendOrganizationInviteQuery : IQuery<Unit, ResendOrganizationInviteQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public OrganizationInviteId InviteId { get; set; } = default!;
}
