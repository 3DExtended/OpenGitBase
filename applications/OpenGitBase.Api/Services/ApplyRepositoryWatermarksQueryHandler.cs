using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class ApplyRepositoryWatermarksQueryHandler
    : IQueryHandler<ApplyRepositoryWatermarksQuery, ApplyRepositoryWatermarksResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ApplyRepositoryWatermarksQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<ApplyRepositoryWatermarksResult>> RunQueryAsync(
        ApplyRepositoryWatermarksQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryWatermarks is not { Count: > 0 })
        {
            return Option.From(new ApplyRepositoryWatermarksResult { AppliedCount = 0 });
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var node = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);

        if (node is null)
        {
            return Option<ApplyRepositoryWatermarksResult>.None;
        }

        var repositoryIds = query.RepositoryWatermarks.Select(report => report.RepositoryId).ToList();
        var replicas = await context
            .Set<RepositoryReplicaEntity>()
            .Where(replica =>
                replica.StorageNodeId == node.Id
                && repositoryIds.Contains(replica.RepositoryId)
            )
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var appliedCount = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var report in query.RepositoryWatermarks)
        {
            var replica = replicas.FirstOrDefault(entry =>
                entry.RepositoryId == report.RepositoryId
            );
            if (replica is null || replica.AppliedWatermark >= report.AppliedWatermark)
            {
                continue;
            }

            replica.AppliedWatermark = report.AppliedWatermark;
            replica.LastSyncedAt = now;
            appliedCount++;
        }

        if (appliedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Option.From(new ApplyRepositoryWatermarksResult { AppliedCount = appliedCount });
    }
}
