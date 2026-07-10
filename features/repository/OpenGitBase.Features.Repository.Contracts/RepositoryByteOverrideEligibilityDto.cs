namespace OpenGitBase.Features.Repository.Contracts;

public sealed class RepositoryByteOverrideEligibilityDto
{
    public bool Eligible { get; set; }

    public string Reason { get; set; } = string.Empty;

    public long? CurrentOverride { get; set; }

    public long MaxAllowedOverride { get; set; }

    public int OrgContributedNodeCount { get; set; }
}
