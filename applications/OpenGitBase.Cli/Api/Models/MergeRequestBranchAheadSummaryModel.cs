namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestBranchAheadSummaryModel
{
    public int AheadCount { get; set; }

    public string? DefaultRef { get; set; }

    public bool HasActiveMergeRequest { get; set; }
}
