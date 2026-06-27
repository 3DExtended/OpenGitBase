namespace OpenGitBase.Features.MergeRequest.Contracts;

public enum MergeRequestMergeStrategy
{
    MergeCommit = 0,
    Squash = 1,
    FastForward = 2,
}
