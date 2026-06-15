namespace OpenGitBase.Features.Organization.Contracts;

public class OrganizationDeleteBlockedResult
{
    public bool Success { get; set; }

    public List<OrganizationDeleteBlocker> Blockers { get; set; } = [];
}
