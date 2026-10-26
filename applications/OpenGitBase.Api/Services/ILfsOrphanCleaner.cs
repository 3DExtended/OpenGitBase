using OpenGitBase.Common.Data;

namespace OpenGitBase.Api.Services;

/// <summary>
/// The reconciler step "LFS orphans": deletes LFS data that storage nodes hold for repositories that no longer
/// exist, such as a node that was down while a repository was deleted.
/// </summary>
public interface ILfsOrphanCleaner
{
    Task RunOnceAsync(OpenGitBaseDbContext context, CancellationToken cancellationToken);
}
