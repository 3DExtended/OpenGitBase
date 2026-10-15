namespace OpenGitBase.Api.Services;

/// <summary>
/// Asks for an LFS reconcile run of a repository (<see cref="ILfsCopyReconciler"/>) in the background. Used by the
/// reconciler pass and by downloads that found no copy.
/// </summary>
public interface ILfsRepairQueue
{
    /// <summary>Queues the repository unless it is already waiting; returns at once.</summary>
    void Schedule(Guid repositoryId);
}
