using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class CommitReplicationWatermarkQueryHandler
    : IQueryHandler<CommitReplicationWatermarkQuery, CommitReplicationWatermarkResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public CommitReplicationWatermarkQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<CommitReplicationWatermarkResult>> RunQueryAsync(
        CommitReplicationWatermarkQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .FirstOrDefaultAsync(
                repository => repository.Id == query.RepositoryId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed("Repository was not found.")
            );
        }

        if (entity.PrimaryStorageNodeId != query.StorageNodeId.Value)
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed(
                    "Only the primary storage node may commit watermarks."
                )
            );
        }

        if (entity.ReplicationEpoch != query.ReplicationEpoch)
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed("Replication epoch is stale.")
            );
        }

        if (query.NewWatermark != entity.PrimaryWatermark + 1)
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed(
                    "Watermark must increment monotonically by one."
                )
            );
        }

        var quorumIds = query.QuorumNodeIds.Select(id => id.Value).Distinct().ToHashSet();
        if (quorumIds.Count < 2)
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed(
                    "At least two quorum members are required."
                )
            );
        }

        var replicaNodeIds = entity.Replicas.Select(replica => replica.StorageNodeId).ToHashSet();
        if (!quorumIds.IsSubsetOf(replicaNodeIds))
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed("Quorum node ids are invalid.")
            );
        }

        if (!quorumIds.Contains(entity.PrimaryStorageNodeId!.Value))
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed("Primary must be included in quorum.")
            );
        }

        // Compare-and-set: the watermark only moves if nobody else moved it since we read it.
        // A push hook and the reconciler (on either API instance) can race for the same
        // watermark; exactly one wins, and a stale writer can never put it back down.
        var expectedWatermark = query.NewWatermark - 1;
        var expectedEpoch = query.ReplicationEpoch;
        var primaryNodeId = query.StorageNodeId.Value;
        var committed = await context
            .Set<RepositoryEntity>()
            .Where(repository =>
                repository.Id == query.RepositoryId.Value
                && repository.PrimaryWatermark == expectedWatermark
                && repository.ReplicationEpoch == expectedEpoch
                && repository.PrimaryStorageNodeId == primaryNodeId
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(repository => repository.PrimaryWatermark, query.NewWatermark),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (committed == 0)
        {
            return Option.From(
                CommitReplicationWatermarkResult.Failed(
                    "Watermark was committed concurrently; it must increment monotonically by one."
                )
            );
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var replica in entity.Replicas.Where(replica =>
                     quorumIds.Contains(replica.StorageNodeId)))
        {
            // Only ever raise: the heartbeat may already report the primary ahead of this commit.
            if (replica.Role == RepositoryReplicaRole.EncryptedReplica)
            {
                replica.ArtifactWatermark = Math.Max(replica.ArtifactWatermark ?? 0, query.NewWatermark);
            }
            else
            {
                replica.AppliedWatermark = Math.Max(replica.AppliedWatermark, query.NewWatermark);
            }

            replica.LastSyncedAt = now;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(CommitReplicationWatermarkResult.Committed(query.NewWatermark));
    }
}
