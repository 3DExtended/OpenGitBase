using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Services;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class StorageRepositoryFileReader : IRepositoryFileReader
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

        try
        {
            return await ReadFromNodeAsync(
                    target,
                    tokenResult.Get(),
                    physicalPath,
                    commitSha,
                    path,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new RepositoryFileUnavailableException(
                $"Storage node {target.StorageNodeId} could not be reached: {ex.Message}",
                ex
            );
        }
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
        // The storage node reports a missing path and a missing commit the same way, so walk the
        // tree from the root instead: a listing that succeeds proves the commit is there, and only
        // then does an absent entry mean the file does not exist.
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var directory = string.Empty;
        for (var index = 0; index < segments.Length; index++)
        {
            var tree = await _storageContentClient
                .GetTreeAsync(target, apiToken, physicalPath, commitSha, directory, cancellationToken)
                .ConfigureAwait(false)
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
