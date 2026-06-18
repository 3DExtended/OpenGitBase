using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.Contracts;

public class AcceptOrganizationInviteQuery
    : IQuery<AcceptOrganizationInviteResult, AcceptOrganizationInviteQuery>
{
    public string Token { get; set; } = string.Empty;

    public UserId UserId { get; set; } = default!;
}
