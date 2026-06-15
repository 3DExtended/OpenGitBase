using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Models;

public class AddOrganizationMemberRequest
{
    public Guid UserId { get; set; }

    public OrganizationMemberRole Role { get; set; } = OrganizationMemberRole.Member;
}
