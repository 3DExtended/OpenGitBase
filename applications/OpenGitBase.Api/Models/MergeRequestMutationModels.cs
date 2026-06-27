#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1201 // Elements should appear in the correct order
namespace OpenGitBase.Api.Models;

public sealed class MergeMergeRequestRequest
{
    public MergeRequestMergeStrategyDto Strategy { get; set; } = MergeRequestMergeStrategyDto.MergeCommit;

    public bool DeleteSourceBranch { get; set; }
}

public enum MergeRequestMergeStrategyDto
{
    MergeCommit = 0,
    Squash = 1,
    FastForward = 2,
}

public sealed class MergeRequestMergeabilityResponse
{
    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }
}

public sealed class CreateMergeRequestDiscussionLinkRequest
{
    public int DiscussionNumber { get; set; }

    public string RelationshipType { get; set; } = "related";
}
