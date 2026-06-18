using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public class RevokeOrganizationInviteQuery : IQuery<Unit, RevokeOrganizationInviteQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public OrganizationInviteId InviteId { get; set; } = default!;
}
