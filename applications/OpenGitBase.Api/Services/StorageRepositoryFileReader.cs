using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>Reads repository files and CI workspaces from the storage node that holds the repository.</summary>
public sealed class StorageRepositoryFileReader : IRepositoryFileReader, IRepositoryArchiveReader
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageContentClient _storageContentClient;

    public StorageRepositoryFileReader(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient
    )
    {
        _queryProcessor = queryProcessor;
        _storageContentClient = storageContentClient;
    }

    public async Task<string?> ReadTextAsync(
        Guid repositoryId,
        string physicalPath,
        string commitSha,
        string path,
        CancellationToken cancellationToken
    )
    {
        var (target, apiToken) = await ResolveNodeAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        try
        {
            return await ReadFromNodeAsync(target, apiToken, physicalPath, commitSha, path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(target, ex);
        }
    }

    public async Task<byte[]> ReadWorkspaceArchiveAsync(
        Guid repositoryId,
        string physicalPath,
        string commitSha,
        int gitDepth,
        CancellationToken cancellationToken
    )
    {
        var (target, apiToken) = await ResolveNodeAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await _storageContentClient
                .GetArchiveAsync(target, apiToken, physicalPath, commitSha, gitDepth, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new RepositoryFileUnavailableException(
                    $"Storage node {target.StorageNodeId} returned {(int)response.StatusCode} for the workspace of {commitSha}."
                );
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(target, ex);
        }
    }

    private static RepositoryFileUnavailableException Unreachable(
        RepositoryRoutingTargetDto target,
        HttpRequestException ex
    ) =>
        new($"Storage node {target.StorageNodeId} could not be reached: {ex.Message}", ex);

    private async Task<(RepositoryRoutingTargetDto Target, string ApiToken)> ResolveNodeAsync(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery { RepositoryId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (routing.IsNone)
        {
            throw new RepositoryFileUnavailableException(
                $"Repository {repositoryId} has no storage routing."
            );
        }

        // A push lands on the primary first, so a replica may not hold the commit yet.
        var target =
            routing.Get().Targets.FirstOrDefault(candidate => candidate.IsPrimary && candidate.IsHealthy)
            ?? routing.Get().Targets.FirstOrDefault(candidate => candidate.IsHealthy)
            ?? throw new RepositoryFileUnavailableException(
                $"No healthy storage node holds repository {repositoryId}."
            );

        var tokenResult = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = StorageNodeId.From(target.StorageNodeId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (tokenResult.IsNone)
        {
            throw new RepositoryFileUnavailableException(
                $"Storage node {target.StorageNodeId} has no API token."
            );
        }

        return (target, tokenResult.Get());
    }

    private async Task<string?> ReadFromNodeAsync(
        RepositoryRoutingTargetDto target,
        string apiToken,
        string physicalPath,
        string commitSha,
        string path,
        CancellationToken cancellationToken
    )
    {
        // Walk the tree from the root: a listing that succeeds proves the node holds the commit,
        // and only then does an absent entry mean the file does not exist. A listing that fails,
        // even as a missing directory, stays unavailable; the walk only descends into directories
        // the parent listing showed, so that answer would be the node's error, not a missing file.
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var directory = string.Empty;
        for (var index = 0; index < segments.Length; index++)
        {
            var listing = await _storageContentClient
                .GetTreeAsync(target, apiToken, physicalPath, commitSha, directory, cancellationToken)
                .ConfigureAwait(false);
            var tree = listing.Payload
                ?? throw new RepositoryFileUnavailableException(
                    $"Storage node {target.StorageNodeId} could not list '{directory}' at {commitSha}."
                );

            var entryPath = directory.Length == 0 ? segments[index] : $"{directory}/{segments[index]}";
            var entryType = index == segments.Length - 1 ? "blob" : "tree";
            if (!tree.Entries.Exists(entry => entry.Path == entryPath && entry.Type == entryType))
            {
                return null;
            }

            directory = entryPath;
        }

        using var response = await _storageContentClient
            .GetRawAsync(target, apiToken, physicalPath, commitSha, directory, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new RepositoryFileUnavailableException(
                $"Storage node {target.StorageNodeId} returned {(int)response.StatusCode} for '{directory}' at {commitSha}."
            );
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
