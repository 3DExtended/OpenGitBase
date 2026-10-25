using System.Text.Json;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Keeps the git part of a repository's storage usage current for the push quota check. Git
/// pushes never wrote <c>StorageBytesUsed</c>, so the check compared against a counter that did
/// not grow. Before a pack is accepted, the bare repository's size is measured on the primary
/// (the storage usage endpoint, <c>du</c>) and recorded, so the check sees what is on disk.
/// </summary>
public sealed class RepositoryGitUsageMeter
{
    /// <summary>The measurement sits in the push access check; a slow node must not hold the push.</summary>
    internal static readonly TimeSpan MeasureTimeout = TimeSpan.FromSeconds(5);

    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageContentClient _storageContentClient;
    private readonly ILogger<RepositoryGitUsageMeter> _logger;

    public RepositoryGitUsageMeter(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        ILogger<RepositoryGitUsageMeter> logger
    )
    {
        _queryProcessor = queryProcessor;
        _storageContentClient = storageContentClient;
        _logger = logger;
    }

    /// <summary>
    /// Measures the repository's git data on <paramref name="primary"/>, records it, and returns
    /// <c>StorageBytesUsed</c> afterwards (git plus everything else counted, such as Git LFS).
    /// When the primary cannot be measured, returns the stored counter unchanged.
    /// </summary>
    public async Task<long> MeasureStorageBytesUsedAsync(
        RepositoryDto repository,
        RepositoryRoutingTargetDto primary,
        CancellationToken cancellationToken
    )
    {
        var gitBytes = await MeasureGitBytesAsync(repository, primary, cancellationToken)
            .ConfigureAwait(false);
        if (gitBytes is null)
        {
            return repository.StorageBytesUsed;
        }

        var recorded = await _queryProcessor
            .RunQueryAsync(
                new RecordRepositoryGitBytesQuery
                {
                    RepositoryId = repository.Id,
                    GitBytes = gitBytes.Value,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return recorded.IsSome ? recorded.Get() : repository.StorageBytesUsed;
    }

    private async Task<long?> MeasureGitBytesAsync(
        RepositoryDto repository,
        RepositoryRoutingTargetDto primary,
        CancellationToken cancellationToken
    )
    {
        var token = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery
                {
                    StorageNodeId = StorageNodeId.From(primary.StorageNodeId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (token.IsNone)
        {
            _logger.LogWarning(
                "No API token for storage node {StorageNodeId}; repository {RepositoryId} is checked against its stored usage.",
                primary.StorageNodeId,
                repository.Id.Value
            );
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MeasureTimeout);
        try
        {
            var payload = await _storageContentClient
                .GetDiskUsageAsync(primary, token.Get(), repository.PhysicalPath, timeout.Token)
                .ConfigureAwait(false);
            if (payload is null || payload.BytesUsed < 0)
            {
                _logger.LogWarning(
                    "Storage node {StorageNodeId} reported no usage for repository {RepositoryId}; it is checked against its stored usage.",
                    primary.StorageNodeId,
                    repository.Id.Value
                );
                return null;
            }

            return payload.BytesUsed;
        }
        catch (Exception ex)
            when (!cancellationToken.IsCancellationRequested
                && ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            _logger.LogWarning(
                ex,
                "Measuring repository {RepositoryId} on storage node {StorageNodeId} failed; it is checked against its stored usage.",
                repository.Id.Value,
                primary.StorageNodeId
            );
            return null;
        }
    }
}
