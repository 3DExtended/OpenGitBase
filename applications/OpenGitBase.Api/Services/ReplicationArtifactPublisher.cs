using Microsoft.Extensions.Options;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Builds RF4 encrypted replication artifacts on a repository's primary and uploads them to
/// encrypted replicas. Used by quorum-replicate (createArtifact, i.e. every push) and by the
/// anti-entropy reconciler, so both produce artifacts the same way.
/// <para>
/// Every hop is an outbound call from the API to a storage node, so no request body flows into
/// the API and Kestrel's body limit never applies. The post-receive hook used to build the
/// artifact itself and POST it, hex-encoded, to /replicate-artifact, which failed with 413 once a
/// bundle passed ~15 MB.
/// </para>
/// <para>
/// Transport failures (node down, timeout) come back as failed results instead of exceptions, so
/// one unreachable node never aborts a caller that is working through several repositories. An
/// upload that takes longer than <see cref="HaStorageBackgroundOptions.ArtifactUploadTimeoutSeconds"/>
/// counts as refused, so a replica that accepts connections but never answers cannot hold a
/// caller until HAProxy or the HttpClient gives up.
/// </para>
/// </summary>
public sealed class ReplicationArtifactPublisher
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly IRepositoryKeyService _repositoryKeyService;
    private readonly TimeSpan _uploadTimeout;

    public ReplicationArtifactPublisher(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IRepositoryKeyService repositoryKeyService,
        IOptions<HaStorageBackgroundOptions> options
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _repositoryKeyService = repositoryKeyService;
        _uploadTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.ArtifactUploadTimeoutSeconds));
    }

    /// <summary>
    /// Bundles the primary's current refs (<c>git bundle --all</c>) and encrypts the bundle there
    /// under <paramref name="watermark"/> and <paramref name="epoch"/>.
    /// </summary>
    public async Task<ReplicationArtifactFetchResult> CreateOnPrimaryAsync(
        StorageNodeDto primaryNode,
        Guid repositoryId,
        string physicalPath,
        long watermark,
        long epoch,
        CancellationToken cancellationToken
    )
    {
        var token = await GetApiTokenAsync(primaryNode.Id, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return ReplicationArtifactFetchResult.Fail(
                401,
                $"Credentials for primary {primaryNode.NodeId} are unavailable."
            );
        }

        var key = await _repositoryKeyService
            .TryGetRepositoryKeyAsync(repositoryId, cancellationToken)
            .ConfigureAwait(false);
        if (key is null)
        {
            return ReplicationArtifactFetchResult.Fail(404, "Repository encryption key is unavailable.");
        }

        try
        {
            var created = await _storageProvisionerClient
                .CreateReplicationArtifactAsync(
                    primaryNode,
                    token,
                    physicalPath,
                    repositoryId,
                    watermark,
                    epoch,
                    Convert.ToHexString(key.KeyMaterial).ToLowerInvariant(),
                    key.KeyVersion,
                    cancellationToken
                )
                .ConfigureAwait(false);
            return created;
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            return ReplicationArtifactFetchResult.Fail(
                503,
                $"Primary {primaryNode.NodeId} unreachable: {ex.Message}"
            );
        }
    }

    /// <summary>Stores <paramref name="artifact"/> on one encrypted replica.</summary>
    public async Task<StorageProvisionerResult> UploadAsync(
        StorageNodeDto targetNode,
        Guid repositoryId,
        long watermark,
        ReplicationArtifactFetchResult artifact,
        CancellationToken cancellationToken
    )
    {
        var token = await GetApiTokenAsync(targetNode.Id, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return StorageProvisionerResult.Fail(
                401,
                $"Credentials for {targetNode.NodeId} are unavailable."
            );
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_uploadTimeout);
        try
        {
            return await _storageProvisionerClient
                .UploadReplicationArtifactAsync(
                    targetNode,
                    token,
                    repositoryId,
                    watermark,
                    artifact.ManifestJson,
                    artifact.BundlePayload,
                    timeout.Token
                )
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StorageProvisionerResult.Fail(
                504,
                $"{targetNode.NodeId} did not store the artifact within {_uploadTimeout.TotalSeconds:0} s"
            );
        }
        catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
        {
            return StorageProvisionerResult.Fail(503, $"{targetNode.NodeId} unreachable: {ex.Message}");
        }
    }

    private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

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
