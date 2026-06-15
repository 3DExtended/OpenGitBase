using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Contracts;

public class ListUserOwnedOrganizationsQuery
    : IQuery<IReadOnlyList<OrganizationSummaryDto>, ListUserOwnedOrganizationsQuery>
{
    public UserId UserId { get; set; } = default!;
}
