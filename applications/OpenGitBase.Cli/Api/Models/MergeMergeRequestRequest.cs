namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeMergeRequestRequest
{
    public MergeRequestMergeStrategy Strategy { get; set; } = MergeRequestMergeStrategy.MergeCommit;

    public bool DeleteSourceBranch { get; set; }
}
