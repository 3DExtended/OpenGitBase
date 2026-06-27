namespace OpenGitBase.Api.Models;

public sealed class MergeRequestBranchAheadSummaryResponse
{
    public int AheadCount { get; set; }
    public string? DefaultRef { get; set; }
    public bool HasActiveMergeRequest { get; set; }
}
