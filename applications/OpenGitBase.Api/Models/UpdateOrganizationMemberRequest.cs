using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Models;

public class UpdateOrganizationMemberRequest
{
    public OrganizationMemberRole Role { get; set; }
}
