#pragma warning disable SA1402 // File may only contain a single type
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest.QueryHandlers;

public class ApproveMergeRequestQueryHandler : IQueryHandler<ApproveMergeRequestQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly MergeRequestGateCoordinator _gateCoordinator;

    public ApproveMergeRequestQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _gateCoordinator = new MergeRequestGateCoordinator(systemClock);
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        ApproveMergeRequestQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || query.Number <= 0
            || query.ApproverUserId.Value == Guid.Empty
        )
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (
            entity is null
            || entity.IsDraft
            || entity.Status is not (int)MergeRequestStatus.Open
            || entity.CreatorUserId == query.ApproverUserId.Value
        )
        {
            return Option<MergeRequestDto>.None;
        }

        var defaultBranch = await LoadDefaultBranchNameAsync(
            query.RepositoryId,
            cancellationToken
        ).ConfigureAwait(false);
        var targetPolicy = await _gateCoordinator
            .LoadTargetPolicyAsync(
                _queryProcessor,
                query.RepositoryId,
                entity.TargetRef,
                defaultBranch,
                cancellationToken
            )
            .ConfigureAwait(false);

        var utcNow = _systemClock.UtcNow;
        var existing = await context
            .Set<MergeRequestApprovalEntity>()
            .FirstOrDefaultAsync(
                approval =>
                    approval.MergeRequestId == entity.Id
                    && approval.UserId == query.ApproverUserId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.Set<MergeRequestApprovalEntity>().Add(
                new MergeRequestApprovalEntity
                {
                    MergeRequestId = entity.Id,
                    UserId = query.ApproverUserId.Value,
                    CommitSha = entity.SourceHeadSha,
                    CreatedAt = utcNow,
                }
            );
        }
        else
        {
            existing.CommitSha = entity.SourceHeadSha;
            existing.CreatedAt = utcNow;
        }

        entity.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _gateCoordinator
            .EvaluateAndTransitionAsync(context, entity, targetPolicy, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            await _gateCoordinator
                .BuildDtoAsync(context, entity, targetPolicy, cancellationToken)
                .ConfigureAwait(false)
        );
    }

    private async Task<string?> LoadDefaultBranchNameAsync(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var repositoryResult = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        return repositoryResult.IsSome ? repositoryResult.Get().DefaultBranchName : null;
    }
}

public class DismissMergeRequestApprovalsQueryHandler
    : IQueryHandler<DismissMergeRequestApprovalsQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly MergeRequestGateCoordinator _gateCoordinator;

    public DismissMergeRequestApprovalsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _gateCoordinator = new MergeRequestGateCoordinator(systemClock);
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        DismissMergeRequestApprovalsQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.RepositoryId == Guid.Empty || query.Number <= 0)
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<MergeRequestDto>.None;
        }

        var defaultBranch = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = RepositoryId.From(query.RepositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        var defaultBranchName = defaultBranch.IsSome
            ? defaultBranch.Get().DefaultBranchName
            : null;
        var targetPolicy = await _gateCoordinator
            .LoadTargetPolicyAsync(
                _queryProcessor,
                query.RepositoryId,
                entity.TargetRef,
                defaultBranchName,
                cancellationToken
            )
            .ConfigureAwait(false);

        var approvals = await context
            .Set<MergeRequestApprovalEntity>()
            .Where(approval => approval.MergeRequestId == entity.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.Set<MergeRequestApprovalEntity>().RemoveRange(approvals);

        if (entity.Status == (int)MergeRequestStatus.Approved)
        {
            entity.Status = (int)MergeRequestStatus.Open;
        }

        entity.UpdatedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            await _gateCoordinator
                .BuildDtoAsync(context, entity, targetPolicy, cancellationToken)
                .ConfigureAwait(false)
        );
    }
}

public class RecordMergeRequestMergedQueryHandler
    : IQueryHandler<RecordMergeRequestMergedQuery, MergeRequestDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _systemClock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly MergeRequestGateCoordinator _gateCoordinator;

    public RecordMergeRequestMergedQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock systemClock,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _systemClock = systemClock;
        _queryProcessor = queryProcessor;
        _gateCoordinator = new MergeRequestGateCoordinator(systemClock);
    }

    public async Task<Option<MergeRequestDto>> RunQueryAsync(
        RecordMergeRequestMergedQuery query,
        CancellationToken cancellationToken
    )
    {
        if (
            query.RepositoryId == Guid.Empty
            || query.Number <= 0
            || string.IsNullOrWhiteSpace(query.MergeCommitSha)
        )
        {
            return Option<MergeRequestDto>.None;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await MergeRequestProjection
            .FindByNumberAsync(context, query.RepositoryId, query.Number, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null || entity.Status is not (int)MergeRequestStatus.Approved)
        {
            return Option<MergeRequestDto>.None;
        }

        entity.Status = (int)MergeRequestStatus.Merged;
        entity.MergeCommitSha = query.MergeCommitSha.Trim();
        entity.IsDraft = false;
        entity.UpdatedAt = _systemClock.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var defaultBranch = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryQuery { ModelId = RepositoryId.From(query.RepositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        var targetPolicy = await _gateCoordinator
            .LoadTargetPolicyAsync(
                _queryProcessor,
                query.RepositoryId,
                entity.TargetRef,
                defaultBranch.IsSome ? defaultBranch.Get().DefaultBranchName : null,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From(
            await _gateCoordinator
                .BuildDtoAsync(context, entity, targetPolicy, cancellationToken)
                .ConfigureAwait(false)
        );
    }
}
