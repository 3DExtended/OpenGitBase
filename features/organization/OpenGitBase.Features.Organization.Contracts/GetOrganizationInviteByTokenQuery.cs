using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public class GetOrganizationInviteByTokenQuery
    : IQuery<OrganizationInvitePublicDto, GetOrganizationInviteByTokenQuery>
{
    public string Token { get; set; } = string.Empty;
}
