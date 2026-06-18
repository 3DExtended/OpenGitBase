using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public class ListOrganizationInvitesQuery
    : IQuery<IReadOnlyList<OrganizationInviteDto>, ListOrganizationInvitesQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public bool RevealEmail { get; set; }
}
