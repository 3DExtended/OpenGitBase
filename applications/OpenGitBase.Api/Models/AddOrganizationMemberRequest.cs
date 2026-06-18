using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Models;

public class AddOrganizationMemberRequest
{
    public string Identifier { get; set; } = string.Empty;

    public OrganizationMemberRole Role { get; set; } = OrganizationMemberRole.Member;
}
