using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public class DeclineOrganizationInviteQuery : IQuery<Unit, DeclineOrganizationInviteQuery>
{
    public string Token { get; set; } = string.Empty;
}
