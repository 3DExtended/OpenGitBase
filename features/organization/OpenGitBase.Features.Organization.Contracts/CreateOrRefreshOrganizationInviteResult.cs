namespace OpenGitBase.Features.Organization.Contracts;

public class CreateOrRefreshOrganizationInviteResult
{
    public OrganizationInviteId InviteId { get; set; } = default!;

    public string Token { get; set; } = string.Empty;
}
