using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Streams LFS objects from the repository's plaintext nodes, read replicas first and the write primary last,
/// like git-lfs downloads. A node that lacks the object or cannot be reached is skipped. Nothing is buffered:
/// the caller gets the storage response stream.
/// </summary>
public sealed class LfsContentReader : ILfsContentReader
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageLfsClient _storage;
    private readonly ILfsRepairQueue _repairs;
    private readonly ILogger<LfsContentReader> _logger;

    public LfsContentReader(
        IQueryProcessor queryProcessor,
        IStorageLfsClient storage,
        ILfsRepairQueue repairs,
        ILogger<LfsContentReader> logger
    )
    {
        _queryProcessor = queryProcessor;
        _storage = storage;
        _repairs = repairs;
        _logger = logger;
    }

    public async Task<long?> FindRecordedSizeAsync(Guid repositoryId, string oid, CancellationToken cancellationToken)
    {
        var recorded = await _queryProcessor
            .RunQueryAsync(
                new FindLfsObjectsQuery { RepositoryId = RepositoryId.From(repositoryId), Oids = [oid] },
                cancellationToken
            )
            .ConfigureAwait(false);
        return recorded.IsSome && recorded.Get().FirstOrDefault(item => item.Oid == oid) is { } found ? found.Size : null;
    }

    public async Task<Stream?> OpenAsync(Guid repositoryId, string oid, CancellationToken cancellationToken)
    {
        foreach (var target in await ReadTargetsAsync(repositoryId, cancellationToken).ConfigureAwait(false))
        {
            var token = await _queryProcessor
                .RunQueryAsync(
                    new GetStorageNodeApiTokenQuery { StorageNodeId = StorageNodeId.From(target.StorageNodeId) },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (token.IsNone)
            {
                continue;
            }

            var node = new StorageNodeEndpoint(target.StorageNodeId, target.InternalHost, target.InternalHttpPort, token.Get());
            try
            {
                var content = await _storage
                    .OpenObjectAsync(node, repositoryId, oid, LfsObjectCopyKind.Plaintext, cancellationToken)
                    .ConfigureAwait(false);
                if (content is not null)
                {
                    return content;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogInformation(ex, "Could not read LFS object {Oid} of {RepositoryId} from {Node}", oid, repositoryId, target.InternalHost);
            }
        }

        _logger.LogWarning("No node holds LFS object {Oid} of {RepositoryId}; queuing a repair", oid, repositoryId);
        _repairs.Schedule(repositoryId);
        return null;
    }

    private async Task<IReadOnlyList<RepositoryRoutingTargetDto>> ReadTargetsAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery { RepositoryId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (routing.IsNone)
        {
            return [];
        }

        // A node can be listed as primary and as read replica; it is asked once.
        return routing
            .Get()
            .Targets.Where(target => target.IsHealthy)
            .OrderBy(target => target.IsPrimary)
            .DistinctBy(target => target.StorageNodeId)
            .ToList();
    }
}
