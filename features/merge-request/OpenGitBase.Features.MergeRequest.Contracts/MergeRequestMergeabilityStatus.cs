namespace OpenGitBase.Features.MergeRequest.Contracts;

public enum MergeRequestMergeabilityStatus
{
    Checking = 0,
    Mergeable = 1,
    Conflicts = 2,
    Unknown = 3,
}
