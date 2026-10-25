namespace OpenGitBase.Api.Services;

/// <summary>Finishes the copies of a verified LFS object after verify has answered.</summary>
public interface ILfsCopyCompletionScheduler
{
    void Schedule(LfsPendingCopies pending);
}
