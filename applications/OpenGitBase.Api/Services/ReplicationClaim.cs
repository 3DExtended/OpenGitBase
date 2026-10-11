using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Timestamps written to the primary replica's LastSyncedAt to mark "replication of this
/// repository is in progress". Truncated to whole milliseconds so a value read back from the
/// database (PostgreSQL keeps microseconds, .NET ticks are 100 ns) compares equal to the value
/// that was written.
/// </summary>
internal static class ReplicationClaim
{
    public static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
    }

    /// <summary>
    /// Marks that a push is replicating the repository now: moves LastSyncedAt of the primary's
    /// replica row (only if <paramref name="primaryNodeId"/> holds the Primary role). The
    /// reconciler leaves a repository marked within its grace period alone and stops a run it
    /// claimed before as soon as the mark moves. True when a row was marked.
    /// </summary>
    public static async Task<bool> MarkPrimaryReplicatingAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        Guid repositoryId,
        Guid primaryNodeId,
        CancellationToken cancellationToken
    )
    {
        var now = Now();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var marked = await db
            .Set<RepositoryReplicaEntity>()
            .Where(replica =>
                replica.RepositoryId == repositoryId
                && replica.StorageNodeId == primaryNodeId
                && replica.Role == RepositoryReplicaRole.Primary
            )
            .ExecuteUpdateAsync(setters => setters.SetProperty(replica => replica.LastSyncedAt, now), cancellationToken)
            .ConfigureAwait(false);
        return marked > 0;
    }
}
