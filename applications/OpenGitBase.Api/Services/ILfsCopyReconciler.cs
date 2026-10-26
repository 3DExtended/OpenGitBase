namespace OpenGitBase.Api.Services;

/// <summary>Compares a repository's recorded LFS objects with what its nodes hold on disk and repairs missing copies.</summary>
public interface ILfsCopyReconciler
{
    Task<LfsReconcileResult> ReconcileRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken);
}
