namespace OpenGitBase.Features.Organization.Contracts;

public class OrganizationDeleteBlocker
{
    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
}
