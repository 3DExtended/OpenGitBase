using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Contracts;

public class GetOrganizationMemberQuery
    : IQuery<OrganizationMemberDto, GetOrganizationMemberQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public UserId UserId { get; set; } = default!;
}
