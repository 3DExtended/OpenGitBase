using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Builds the fleet drift report: probes every healthy storage node's on-disk inventory and
/// cross-references it against the control-plane <c>RepositoryReplica</c> records, surfacing both
/// on-disk copies with no matching record and records with no matching on-disk data. It also
/// checks each repository's primary pointer against its Primary replica row
/// (<see cref="FleetDriftKind.PrimaryMismatch"/>). Read-only — it detects drift, it never remediates.
/// </summary>
public sealed class GetFleetDriftReportQueryHandler
    : IQueryHandler<GetFleetDriftReportQuery, FleetDriftReportDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;

    public GetFleetDriftReportQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
    }

    public async Task<Option<FleetDriftReportDto>> RunQueryAsync(
        GetFleetDriftReportQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var repositories = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Include(repository => repository.Replicas)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var repositoryNameById = repositories.ToDictionary(
            repository => repository.Id,
            repository => repository.Name
        );

        // Recorded expectations, keyed by storage node id.
        var plaintextRecords = new Dictionary<Guid, HashSet<Guid>>();
        var artifactRecords = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var repository in repositories)
        {
            foreach (var replica in repository.Replicas)
            {
                var target =
                    replica.Role == RepositoryReplicaRole.EncryptedReplica
                        ? artifactRecords
                        : plaintextRecords;
                if (!target.TryGetValue(replica.StorageNodeId, out var set))
                {
                    set = [];
                    target[replica.StorageNodeId] = set;
                }

                set.Add(repository.Id);
            }
        }

        var nodesResult = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        var nodes = nodesResult.IsSome ? nodesResult.Get() : [];

        var nodeStatuses = new List<FleetDriftNodeStatusDto>();
        var drift = new List<FleetDriftEntryDto>();

        foreach (var node in nodes)
        {
            var nodeGuid = node.Id.Value;
            var token = await GetApiTokenAsync(node.Id, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                nodeStatuses.Add(
                    new FleetDriftNodeStatusDto
                    {
                        StorageNodeId = nodeGuid,
                        NodeId = node.NodeId,
                        Reachable = false,
                        Error = "Storage node API token is unavailable.",
                    }
                );
                continue;
            }

            var inventory = await _storageProvisionerClient
                .TryGetInventoryAsync(node, token, cancellationToken)
                .ConfigureAwait(false);
            if (!inventory.Success)
            {
                nodeStatuses.Add(
                    new FleetDriftNodeStatusDto
                    {
                        StorageNodeId = nodeGuid,
                        NodeId = node.NodeId,
                        Reachable = false,
                        Error = inventory.Error,
                    }
                );
                continue;
            }

            nodeStatuses.Add(
                new FleetDriftNodeStatusDto
                {
                    StorageNodeId = nodeGuid,
                    NodeId = node.NodeId,
                    Reachable = true,
                }
            );

            var onDiskPlaintext = inventory.PlaintextRepositories.ToHashSet();
            var onDiskArtifacts = inventory.ArtifactRepositories.ToHashSet();
            var recordedPlaintext =
                plaintextRecords.GetValueOrDefault(nodeGuid) ?? [];
            var recordedArtifacts =
                artifactRecords.GetValueOrDefault(nodeGuid) ?? [];

            AddDrift(
                drift,
                onDiskPlaintext.Except(recordedPlaintext),
                node,
                nodeGuid,
                repositoryNameById,
                FleetDriftKind.OrphanOnDisk,
                "Plaintext repo is on disk but has no plaintext replica record on this node."
            );
            AddDrift(
                drift,
                recordedPlaintext.Except(onDiskPlaintext),
                node,
                nodeGuid,
                repositoryNameById,
                FleetDriftKind.MissingOnDisk,
                "Plaintext replica record exists for this node but the repo is not on disk."
            );
            AddDrift(
                drift,
                onDiskArtifacts.Except(recordedArtifacts),
                node,
                nodeGuid,
                repositoryNameById,
                FleetDriftKind.OrphanArtifact,
                "Encrypted artifact is on disk but has no EncryptedReplica record on this node."
            );
            AddDrift(
                drift,
                recordedArtifacts.Except(onDiskArtifacts),
                node,
                nodeGuid,
                repositoryNameById,
                FleetDriftKind.MissingArtifact,
                "EncryptedReplica record exists for this node but no artifact is on disk."
            );
            AddDrift(
                drift,
                inventory.LfsRepositories.Where(repositoryId => !repositoryNameById.ContainsKey(repositoryId)),
                node,
                nodeGuid,
                repositoryNameById,
                FleetDriftKind.OrphanLfs,
                "LFS data is on disk for a repository that no longer exists."
            );
        }

        await AddPrimaryMismatchesAsync(context, drift, repositories, cancellationToken).ConfigureAwait(false);

        return Option.From(
            new FleetDriftReportDto
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                Nodes = nodeStatuses,
                Drift = drift,
            }
        );
    }

    /// <summary>
    /// Repositories whose primary pointer and Primary replica row disagree. Read from the database alone, so reported
    /// whether or not the nodes answer, on the node the pointer names. A legacy repository without a
    /// PrimaryStorageNodeId routes through StorageNodeId alone and is skipped.
    /// </summary>
    private static async Task AddPrimaryMismatchesAsync(
        OpenGitBaseDbContext context,
        List<FleetDriftEntryDto> drift,
        IReadOnlyList<RepositoryEntity> repositories,
        CancellationToken cancellationToken
    )
    {
        var mismatches = repositories
            .Where(repository => repository.PrimaryStorageNodeId is not null)
            .Select(repository => (Repository: repository, Problems: RepositoryPrimaryConsistency.FindProblems(repository)))
            .Where(entry => entry.Problems.Count > 0)
            .ToList();
        if (mismatches.Count == 0)
        {
            return;
        }

        // The pointed node may be down and so missing from the probed nodes; its name comes from the database.
        var nodeNameById = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .ToDictionaryAsync(node => node.Id, node => node.NodeId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (repository, problems) in mismatches)
        {
            var pointedNodeId = repository.PrimaryStorageNodeId!.Value;
            drift.Add(
                new FleetDriftEntryDto
                {
                    StorageNodeId = pointedNodeId,
                    NodeId = nodeNameById.GetValueOrDefault(pointedNodeId) ?? string.Empty,
                    RepositoryId = repository.Id,
                    RepositoryName = repository.Name,
                    Kind = FleetDriftKind.PrimaryMismatch,
                    Detail = string.Join(" ", problems),
                }
            );
        }
    }

    private static void AddDrift(
        List<FleetDriftEntryDto> drift,
        IEnumerable<Guid> repositoryIds,
        Features.StorageNode.Contracts.StorageNodeDto node,
        Guid nodeGuid,
        IReadOnlyDictionary<Guid, string> repositoryNameById,
        FleetDriftKind kind,
        string detail
    )
    {
        foreach (var repositoryId in repositoryIds)
        {
            drift.Add(
                new FleetDriftEntryDto
                {
                    StorageNodeId = nodeGuid,
                    NodeId = node.NodeId,
                    RepositoryId = repositoryId,
                    RepositoryName = repositoryNameById.GetValueOrDefault(repositoryId),
                    Kind = kind,
                    Detail = detail,
                }
            );
        }
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
