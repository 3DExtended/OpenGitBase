using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class Rf1BackfillService
{
    private const int MtlsGitHttpPort = 8443;

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;

    public Rf1BackfillService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .Where(repository =>
                repository.Replicas.Count < 3
                && repository.ReplicationState != ReplicationState.Rf3Healthy
            )
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repository in candidates)
        {
            await BackfillRepositoryAsync(context, repository, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task BackfillRepositoryAsync(
        OpenGitBaseDbContext context,
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        if (repository.StorageNodeId is null)
        {
            return;
        }

        repository.ReplicationState = ReplicationState.Rf1Backfilling;
        repository.PrimaryStorageNodeId ??= repository.StorageNodeId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var healthyNodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        var nodes = healthyNodes.IsSome ? healthyNodes.Get() : Array.Empty<StorageNodeDto>();
        var existingNodeIds = repository.Replicas.Select(replica => replica.StorageNodeId).ToHashSet();
        if (existingNodeIds.Count == 0)
        {
            existingNodeIds.Add(repository.StorageNodeId.Value);
            repository.Replicas.Add(
                new RepositoryReplicaEntity
                {
                    RepositoryId = repository.Id,
                    StorageNodeId = repository.StorageNodeId.Value,
                    Role = RepositoryReplicaRole.Primary,
                    AppliedWatermark = repository.PrimaryWatermark,
                }
            );
        }

        var additionalNodes = nodes
            .Where(node => !existingNodeIds.Contains(node.Id.Value))
            .OrderByDescending(node => node.FreeBytesAvailable)
            .Take(Math.Max(0, 3 - existingNodeIds.Count))
            .ToList();

        if (additionalNodes.Count == 0 && existingNodeIds.Count < 3)
        {
            repository.ReplicationState = ReplicationState.Degraded;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var node in additionalNodes)
        {
            var token = await GetApiTokenAsync(node.Id, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                continue;
            }

            var primaryNode = nodes.First(node =>
                node.Id.Value == repository.PrimaryStorageNodeId
            );

            var provision = await _storageProvisionerClient
                .ProvisionRepositoryAsync(
                    node,
                    token,
                    repository.PhysicalPath,
                    receiveMaxBytes: 0,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!provision.Success)
            {
                continue;
            }

            await _storageProvisionerClient
                .SyncRepositoryFromPeerAsync(
                    node,
                    token,
                    repository.PhysicalPath,
                    primaryNode.InternalHost,
                    repository.PhysicalPath,
                    MtlsGitHttpPort,
                    cancellationToken
                )
                .ConfigureAwait(false);

            repository.Replicas.Add(
                new RepositoryReplicaEntity
                {
                    RepositoryId = repository.Id,
                    StorageNodeId = node.Id.Value,
                    Role = RepositoryReplicaRole.Replica,
                    AppliedWatermark = repository.PrimaryWatermark,
                    LastSyncedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        if (repository.Replicas.Count >= 3)
        {
            repository.ReplicationState = ReplicationState.Rf3Healthy;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetApiTokenAsync(
        StorageNodeId storageNodeId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? result.Get() : null;
    }
}
