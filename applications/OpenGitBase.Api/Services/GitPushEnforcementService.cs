using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

public sealed class GitPushEnforcementService
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageContentClient _storageContentClient;
    private readonly WebReadReplicaSelector _replicaSelector;

    public GitPushEnforcementService(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        WebReadReplicaSelector replicaSelector
    )
    {
        _queryProcessor = queryProcessor;
        _storageContentClient = storageContentClient;
        _replicaSelector = replicaSelector;
    }

    public static GitPushEnforcementResult EvaluateProtectedRefUpdate(
        ProtectedBranchRuleDto rule,
        GitRefUpdateRequest refUpdate,
        UserId userId,
        RepositoryRole role,
        bool isRepositoryOwner,
        bool isPlatformMergeIdentity
    )
    {
        if (isPlatformMergeIdentity)
        {
            return GitPushEnforcementResult.Allow();
        }

        if (refUpdate.IsForcePush == true)
        {
            var forcePushResult = EvaluateForcePushPolicy(
                rule,
                userId,
                role,
                isRepositoryOwner
            );
            if (!forcePushResult.Allowed)
            {
                return forcePushResult;
            }
        }

        if (!RequiresDirectPushBlock(rule))
        {
            return GitPushEnforcementResult.Allow();
        }

        if (IsAllowlistedPusher(rule, userId, role, isRepositoryOwner))
        {
            return GitPushEnforcementResult.Allow();
        }

        var branchName = GitShaHelper.TryGetBranchName(refUpdate.RefName) ?? refUpdate.RefName;
        return GitPushEnforcementResult.Deny(
            $"Direct push to protected branch '{branchName}' is not allowed. "
                + "Use a merge request or ask an administrator to add you to the push allowlist."
        );
    }

    public static bool RequiresDirectPushBlock(ProtectedBranchRuleDto rule) =>
        rule.BlockDirectPush || rule.RequireMergeRequest;

    public static bool IsAllowlistedPusher(
        ProtectedBranchRuleDto rule,
        UserId userId,
        RepositoryRole role,
        bool isRepositoryOwner
    )
    {
        if (rule.AllowedPushUserIds.Any(id => id == userId))
        {
            return true;
        }

        if (isRepositoryOwner && rule.AllowedPushRoles.HasFlag(AllowedPushRoles.Owner))
        {
            return true;
        }

        if (role >= RepositoryRole.Admin && rule.AllowedPushRoles.HasFlag(AllowedPushRoles.Admin))
        {
            return true;
        }

        if (role >= RepositoryRole.Writer && rule.AllowedPushRoles.HasFlag(AllowedPushRoles.Writer))
        {
            return true;
        }

        return false;
    }

    public static GitPushEnforcementResult EvaluateForcePushPolicy(
        ProtectedBranchRuleDto rule,
        UserId userId,
        RepositoryRole role,
        bool isRepositoryOwner
    ) =>
        rule.ForcePushPolicy switch
        {
            ForcePushPolicy.DenyAll => GitPushEnforcementResult.Deny(
                "Force-push is denied by protected branch policy."
            ),
            ForcePushPolicy.AllowAllowedPushers when !IsAllowlistedPusher(
                rule,
                userId,
                role,
                isRepositoryOwner
            ) =>
                GitPushEnforcementResult.Deny(
                    "Force-push is denied by protected branch policy."
                ),
            ForcePushPolicy.PlatformOnly => GitPushEnforcementResult.Deny(
                "Force-push is denied by protected branch policy."
            ),
            _ => GitPushEnforcementResult.Allow(),
        };

    public async Task<GitPushEnforcementResult> EvaluatePushRulesOnlyAsync(
        RepositoryDto repository,
        IReadOnlyList<GitPushCommitRequest> commits,
        CancellationToken cancellationToken
    )
    {
        if (commits.Count == 0)
        {
            return GitPushEnforcementResult.Allow();
        }

        var rulesResult = await _queryProcessor
            .RunQueryAsync(
                new ListProtectedBranchRulesQuery { RepositoryId = repository.Id },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (rulesResult.IsNone)
        {
            return GitPushEnforcementResult.Allow();
        }

        var pushRules = rulesResult
            .Get()
            .SelectMany(rule => rule.PushRules)
            .GroupBy(rule => rule.Id.Value)
            .Select(group => group.First())
            .ToList();

        return PushRuleEvaluator.EvaluateCommits(pushRules, commits);
    }

    public async Task<GitPushEnforcementResult> EvaluateAsync(
        RepositoryDto repository,
        UserId userId,
        RepositoryRole role,
        bool isRepositoryOwner,
        bool isPlatformMergeIdentity,
        IReadOnlyList<GitRefUpdateRequest> refUpdates,
        IReadOnlyList<GitPushCommitRequest> commits,
        CancellationToken cancellationToken
    )
    {
        if (refUpdates.Count == 0)
        {
            return GitPushEnforcementResult.Allow();
        }

        var enrichedRefUpdates = await EnrichForcePushFlagsAsync(
                repository,
                refUpdates,
                cancellationToken
            )
            .ConfigureAwait(false);

        var rulesResult = await _queryProcessor
            .RunQueryAsync(
                new ListProtectedBranchRulesQuery { RepositoryId = repository.Id },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (rulesResult.IsNone)
        {
            return GitPushEnforcementResult.Allow();
        }

        var rules = rulesResult.Get();
        if (rules.Count == 0)
        {
            return GitPushEnforcementResult.Allow();
        }

        var pushRulesToEvaluate = new List<PushRuleDto>();

        foreach (var refUpdate in enrichedRefUpdates)
        {
            var branchName = GitShaHelper.TryGetBranchName(refUpdate.RefName);
            if (branchName is null)
            {
                continue;
            }

            var matchingRules = rules
                .Where(rule =>
                    RepositoryBranchPatternMatcher.Matches(
                        branchName,
                        rule.Pattern,
                        repository.DefaultBranchName
                    )
                )
                .ToList();

            if (matchingRules.Count == 0)
            {
                continue;
            }

            foreach (var rule in matchingRules)
            {
                var protectedBranchResult = EvaluateProtectedRefUpdate(
                    rule,
                    refUpdate,
                    userId,
                    role,
                    isRepositoryOwner,
                    isPlatformMergeIdentity
                );

                if (!protectedBranchResult.Allowed)
                {
                    return protectedBranchResult;
                }

                pushRulesToEvaluate.AddRange(rule.PushRules);
            }
        }

        var distinctPushRules = pushRulesToEvaluate
            .GroupBy(rule => rule.Id.Value)
            .Select(group => group.First())
            .ToList();

        return PushRuleEvaluator.EvaluateCommits(distinctPushRules, commits);
    }

    internal async Task<IReadOnlyList<GitRefUpdateRequest>> EnrichForcePushFlagsAsync(
        RepositoryDto repository,
        IReadOnlyList<GitRefUpdateRequest> refUpdates,
        CancellationToken cancellationToken
    )
    {
        var enriched = refUpdates
            .Select(update => new GitRefUpdateRequest
            {
                RefName = update.RefName,
                OldSha = update.OldSha,
                NewSha = update.NewSha,
                IsForcePush = update.IsForcePush,
            })
            .ToList();

        var needsGitCheck = enriched.Any(update =>
            update.IsForcePush is null && GitShaHelper.NeedsForcePushCheck(update.OldSha, update.NewSha)
        );
        if (!needsGitCheck)
        {
            return enriched;
        }

        var context = await LoadStorageContextAsync(repository, cancellationToken).ConfigureAwait(false);
        if (context is null)
        {
            return enriched;
        }

        foreach (var update in enriched)
        {
            if (update.IsForcePush is not null)
            {
                continue;
            }

            if (!GitShaHelper.NeedsForcePushCheck(update.OldSha, update.NewSha))
            {
                update.IsForcePush = false;
                continue;
            }

            var isAncestor = await _storageContentClient
                .IsAncestorAsync(
                    context.Target,
                    context.ApiToken,
                    context.PhysicalPath,
                    update.OldSha,
                    update.NewSha,
                    cancellationToken
                )
                .ConfigureAwait(false);

            update.IsForcePush = isAncestor is null ? null : !isAncestor.Value;
        }

        return enriched;
    }

    private async Task<StorageContentContext?> LoadStorageContextAsync(
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

        return new StorageContentContext
        {
            Target = selection.Target,
            ApiToken = tokenResult.Get(),
            PhysicalPath = repository.PhysicalPath,
        };
    }

    private sealed class StorageContentContext
    {
        public required RepositoryRoutingTargetDto Target { get; init; }

        public required string ApiToken { get; init; }

        public required string PhysicalPath { get; init; }
    }
}