#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class MergeRequestRefResolution
{
    public required string SourceHeadSha { get; init; }
    public required string TargetBaseSha { get; init; }
}

public sealed class MergeRequestBranchAheadSummary
{
    public int AheadCount { get; init; }
    public string? DefaultRef { get; init; }
    public bool HasActiveMergeRequest { get; init; }
}

public sealed class MergeRequestRefService
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageContentClient _storageContentClient;
    private readonly WebReadReplicaSelector _replicaSelector;

    public MergeRequestRefService(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        WebReadReplicaSelector replicaSelector
    )
    {
        _queryProcessor = queryProcessor;
        _storageContentClient = storageContentClient;
        _replicaSelector = replicaSelector;
    }

    public async Task<MergeRequestRefResolution?> ResolveRefShasAsync(
        RepositoryDto repository,
        string sourceRef,
        string targetRef,
        CancellationToken cancellationToken
    )
    {
        var context = await LoadStorageContextInternalAsync(repository, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        var sourceTask = _storageContentClient.ResolveRefAsync(
            context.Target,
            context.ApiToken,
            context.PhysicalPath,
            sourceRef,
            cancellationToken
        );
        var targetTask = _storageContentClient.ResolveRefAsync(
            context.Target,
            context.ApiToken,
            context.PhysicalPath,
            targetRef,
            cancellationToken
        );

        await Task.WhenAll(sourceTask, targetTask).ConfigureAwait(false);

        var sourceSha = sourceTask.Result?.CommitSha;
        var targetSha = targetTask.Result?.CommitSha;
        if (string.IsNullOrWhiteSpace(sourceSha) || string.IsNullOrWhiteSpace(targetSha))
        {
            return null;
        }

        return new MergeRequestRefResolution
        {
            SourceHeadSha = sourceSha,
            TargetBaseSha = targetSha,
        };
    }

    public async Task<int?> CountAheadAsync(
        RepositoryDto repository,
        string baseRef,
        string headRef,
        CancellationToken cancellationToken
    )
    {
        var context = await LoadStorageContextInternalAsync(repository, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        var payload = await _storageContentClient
            .GetAheadCountAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                baseRef,
                headRef,
                cancellationToken
            )
            .ConfigureAwait(false);

        return payload?.AheadCount;
    }

    public async Task<MergeRequestBranchAheadSummary?> GetBranchAheadSummaryAsync(
        RepositoryDto repository,
        string refName,
        CancellationToken cancellationToken
    )
    {
        var context = await LoadStorageContextInternalAsync(repository, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return null;
        }

        var refs = await _storageContentClient
            .GetRefsAsync(context.Target, context.ApiToken, context.PhysicalPath, cancellationToken)
            .ConfigureAwait(false);
        if (refs is null)
        {
            return null;
        }

        var branches = refs.Branches
            .Select(item => new RepositoryContentRefDto
            {
                Name = item.Name,
                CommitSha = item.CommitSha,
            })
            .ToList();
        var defaultRef = DefaultRefResolver.Resolve(branches, repository.DefaultBranchName);
        if (string.IsNullOrWhiteSpace(defaultRef))
        {
            return new MergeRequestBranchAheadSummary
            {
                AheadCount = 0,
                DefaultRef = null,
                HasActiveMergeRequest = false,
            };
        }

        var aheadCount = await CountAheadAsync(repository, defaultRef, refName, cancellationToken)
            .ConfigureAwait(false);
        if (aheadCount is null)
        {
            return null;
        }

        var activeResult = await _queryProcessor
            .RunQueryAsync(
                new ListMergeRequestsByRepositoryQuery { RepositoryId = repository.Id.Value },
                cancellationToken
            )
            .ConfigureAwait(false);

        var hasActiveMergeRequest = activeResult.IsSome
            && activeResult.Get().Any(mr =>
                string.Equals(mr.SourceRef, refName, StringComparison.OrdinalIgnoreCase)
                && mr.Status is MergeRequestStatus.Draft or MergeRequestStatus.Open or MergeRequestStatus.Approved
            );

        return new MergeRequestBranchAheadSummary
        {
            AheadCount = aheadCount.Value,
            DefaultRef = defaultRef,
            HasActiveMergeRequest = hasActiveMergeRequest,
        };
    }

    public Task<MergeRequestStorageContext?> LoadStorageContextAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    ) => LoadStorageContextInternalAsync(repository, cancellationToken);

    private async Task<MergeRequestStorageContext?> LoadStorageContextInternalAsync(
        RepositoryDto repository,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(repository.PhysicalPath))
        {
            return null;
        }

        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery { RepositoryId = repository.Id },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (routing.IsNone)
        {
            return null;
        }

        var selection = _replicaSelector.Select(routing.Get());
        if (selection is null)
        {
            return null;
        }

        var tokenResult = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery
                {
                    StorageNodeId = StorageNodeId.From(selection.Target.StorageNodeId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (tokenResult.IsNone)
        {
            return null;
        }

        return new MergeRequestStorageContext
        {
            Target = selection.Target,
            ApiToken = tokenResult.Get(),
            PhysicalPath = repository.PhysicalPath,
        };
    }
}

public sealed class MergeRequestStorageContext
{
    public required RepositoryRoutingTargetDto Target { get; init; }

    public required string ApiToken { get; init; }

    public required string PhysicalPath { get; init; }
}
