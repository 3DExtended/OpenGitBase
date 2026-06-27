#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class MergeRequestMergeResult
{
    public bool Success { get; init; }

    public MergeRequestDto? MergeRequest { get; init; }

    public string? Error { get; init; }

    public int StatusCode { get; init; } = StatusCodes.Status400BadRequest;

    public static MergeRequestMergeResult Ok(MergeRequestDto mergeRequest) =>
        new() { Success = true, MergeRequest = mergeRequest };

    public static MergeRequestMergeResult Fail(string error, int statusCode) =>
        new() { Success = false, Error = error, StatusCode = statusCode };
}

public sealed class MergeRequestMergeService
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageContentClient _storageContentClient;
    private readonly MergeRequestRefService _refService;
    private readonly PlatformMergeIdentityOptions _platformMergeOptions;

    public MergeRequestMergeService(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        MergeRequestRefService refService,
        PlatformMergeIdentityOptions platformMergeOptions
    )
    {
        _queryProcessor = queryProcessor;
        _storageContentClient = storageContentClient;
        _refService = refService;
        _platformMergeOptions = platformMergeOptions;
    }

    public async Task<MergeRequestMergeabilityResponse?> GetMergeabilityAsync(
        RepositoryDto repository,
        MergeRequestDto mergeRequest,
        CancellationToken cancellationToken
    )
    {
        var context = await LoadPrimaryWriteContextAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return new MergeRequestMergeabilityResponse
            {
                Status = nameof(MergeRequestMergeabilityStatus.Unknown),
                Message = "Storage unavailable.",
            };
        }

        var resolved = await _refService
            .ResolveRefShasAsync(
                repository,
                mergeRequest.SourceRef,
                mergeRequest.TargetRef,
                cancellationToken
            )
            .ConfigureAwait(false);

        var targetSha = resolved?.TargetBaseSha ?? mergeRequest.TargetBaseSha;
        var sourceSha = resolved?.SourceHeadSha ?? mergeRequest.SourceHeadSha;

        var payload = await _storageContentClient
            .CheckMergeabilityAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                targetSha,
                sourceSha,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (payload is null)
        {
            return new MergeRequestMergeabilityResponse
            {
                Status = nameof(MergeRequestMergeabilityStatus.Unknown),
                Message = "Unable to check mergeability.",
            };
        }

        return new MergeRequestMergeabilityResponse
        {
            Status = MergeRequestMergeStrategyHelper.MapMergeabilityStatus(payload.Status),
            Message = payload.Status == "conflicts" ? "Merge has conflicts." : null,
        };
    }

    public async Task<MergeRequestMergeResult> MergeAsync(
        RepositoryDto repository,
        MergeRequestDto mergeRequest,
        RepositoryRole actorRole,
        MergeRequestMergeStrategyDto strategy,
        bool deleteSourceBranch,
        CancellationToken cancellationToken
    )
    {
        if (mergeRequest.Status != MergeRequestStatus.Approved)
        {
            return MergeRequestMergeResult.Fail(
                "Merge request must be Approved before merging.",
                StatusCodes.Status400BadRequest
            );
        }

        if (string.IsNullOrWhiteSpace(_platformMergeOptions.AccessToken))
        {
            return MergeRequestMergeResult.Fail(
                "Platform merge identity is not configured.",
                StatusCodes.Status503ServiceUnavailable
            );
        }

        var targetPolicy = MergeRequestTargetPolicyResolver.Resolve(
            mergeRequest.TargetRef,
            repository.DefaultBranchName,
            await LoadProtectedBranchRulesAsync(repository.Id.Value, cancellationToken)
                .ConfigureAwait(false)
        );

        if ((int)actorRole < targetPolicy.MergeRoleThreshold)
        {
            return MergeRequestMergeResult.Fail(
                "Insufficient repository role to merge.",
                StatusCodes.Status403Forbidden
            );
        }

        var effectiveStrategy = MergeRequestMergeStrategyHelper.ResolveStrategy(
            strategy,
            targetPolicy.LockedMergeStrategy
        );
        if (effectiveStrategy is null)
        {
            return MergeRequestMergeResult.Fail(
                "Selected merge strategy is locked by branch protection policy.",
                StatusCodes.Status400BadRequest
            );
        }

        var context = await LoadPrimaryWriteContextAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return MergeRequestMergeResult.Fail(
                "Storage unavailable.",
                StatusCodes.Status503ServiceUnavailable
            );
        }

        var resolved = await _refService
            .ResolveRefShasAsync(
                repository,
                mergeRequest.SourceRef,
                mergeRequest.TargetRef,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (resolved is null)
        {
            return MergeRequestMergeResult.Fail(
                "Unable to resolve merge refs.",
                StatusCodes.Status400BadRequest
            );
        }

        var mergeability = await _storageContentClient
            .CheckMergeabilityAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                resolved.TargetBaseSha,
                resolved.SourceHeadSha,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (mergeability is null)
        {
            return MergeRequestMergeResult.Fail(
                "Unable to verify mergeability.",
                StatusCodes.Status503ServiceUnavailable
            );
        }

        if (string.Equals(mergeability.Status, "conflicts", StringComparison.OrdinalIgnoreCase))
        {
            return MergeRequestMergeResult.Fail(
                "Merge has conflicts.",
                StatusCodes.Status409Conflict
            );
        }

        var executeResult = await _storageContentClient
            .ExecuteMergeAsync(
                context.Target,
                context.ApiToken,
                context.PhysicalPath,
                new StorageContentExecuteMergeRequest
                {
                    TargetRef = mergeRequest.TargetRef,
                    SourceRef = mergeRequest.SourceRef,
                    Strategy = MergeRequestMergeStrategyHelper.ToStorageStrategy(effectiveStrategy.Value),
                    CommitMessage = $"Merge !{mergeRequest.Number}: {mergeRequest.Title}",
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!executeResult.Success || string.IsNullOrWhiteSpace(executeResult.CommitSha))
        {
            return MergeRequestMergeResult.Fail(
                executeResult.ErrorMessage ?? "Merge execution failed.",
                executeResult.StatusCode is >= 400 and < 600
                    ? executeResult.StatusCode
                    : StatusCodes.Status409Conflict
            );
        }

        var recorded = await _queryProcessor
            .RunQueryAsync(
                new RecordMergeRequestMergedQuery
                {
                    RepositoryId = repository.Id.Value,
                    Number = mergeRequest.Number,
                    MergeCommitSha = executeResult.CommitSha,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (recorded.IsNone)
        {
            return MergeRequestMergeResult.Fail(
                "Merge completed in storage but merge request status could not be updated.",
                StatusCodes.Status409Conflict
            );
        }

        await ResolveClosesDiscussionLinksAsync(
            repository.Id.Value,
            mergeRequest.Number,
            cancellationToken
        ).ConfigureAwait(false);

        if (deleteSourceBranch)
        {
            await _storageContentClient
                .DeleteRefAsync(
                    context.Target,
                    context.ApiToken,
                    context.PhysicalPath,
                    mergeRequest.SourceRef,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return MergeRequestMergeResult.Ok(recorded.Get());
    }

    private async Task ResolveClosesDiscussionLinksAsync(
        Guid repositoryId,
        int mergeRequestNumber,
        CancellationToken cancellationToken
    )
    {
        var linksResult = await _queryProcessor
            .RunQueryAsync(
                new ListMergeRequestDiscussionLinksQuery
                {
                    RepositoryId = repositoryId,
                    Number = mergeRequestNumber,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (linksResult.IsNone)
        {
            return;
        }

        foreach (
            var link in linksResult
                .Get()
                .Where(link => link.RelationshipType == MergeRequestRelationshipType.Closes)
        )
        {
            await _queryProcessor
                .RunQueryAsync(
                    new ResolveDiscussionQuery
                    {
                        RepositoryId = repositoryId,
                        Number = link.DiscussionNumber,
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<ProtectedBranchRuleDto>> LoadProtectedBranchRulesAsync(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var rulesResult = await _queryProcessor
            .RunQueryAsync(
                new ListProtectedBranchRulesQuery { RepositoryId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        return rulesResult.IsSome ? rulesResult.Get() : [];
    }

    private async Task<StorageWriteContext?> LoadPrimaryWriteContextAsync(
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

        var primary = routing
            .Get()
            .Targets.FirstOrDefault(target => target.IsPrimary && target.IsHealthy)
            ?? routing.Get().Targets.FirstOrDefault(target => target.IsHealthy);

        if (primary is null)
        {
            return null;
        }

        var tokenResult = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery
                {
                    StorageNodeId = StorageNodeId.From(primary.StorageNodeId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (tokenResult.IsNone)
        {
            return null;
        }

        return new StorageWriteContext
        {
            Target = primary,
            ApiToken = tokenResult.Get(),
            PhysicalPath = repository.PhysicalPath,
        };
    }

    private sealed class StorageWriteContext
    {
        public required RepositoryRoutingTargetDto Target { get; init; }

        public required string ApiToken { get; init; }

        public required string PhysicalPath { get; init; }
    }
}
