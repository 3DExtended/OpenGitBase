using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.MergeRequest;

internal sealed class MergeRequestGateCoordinator
{
    private readonly ISystemClock _systemClock;
    private readonly MergeGateRegistry _gateRegistry;

    public MergeRequestGateCoordinator(ISystemClock systemClock)
        : this(systemClock, MergeGateRegistry.CreateDefault())
    {
    }

    internal MergeRequestGateCoordinator(ISystemClock systemClock, MergeGateRegistry gateRegistry)
    {
        _systemClock = systemClock;
        _gateRegistry = gateRegistry;
    }

    public async Task<MergeRequestTargetPolicy> LoadTargetPolicyAsync(
        IQueryProcessor queryProcessor,
        Guid repositoryId,
        string targetRef,
        string? defaultBranchName,
        CancellationToken cancellationToken
    )
    {
        var rulesResult = await queryProcessor
            .RunQueryAsync(
                new ListProtectedBranchRulesQuery { RepositoryId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        var rules = rulesResult.IsSome ? rulesResult.Get() : [];
        return MergeRequestTargetPolicyResolver.Resolve(targetRef, defaultBranchName, rules);
    }

    public async Task<IReadOnlyList<MergeRequestApprovalDto>> LoadApprovalsAsync(
        OpenGitBaseDbContext context,
        Guid mergeRequestId,
        CancellationToken cancellationToken
    )
    {
        var entities = await context
            .Set<MergeRequestApprovalEntity>()
            .Where(entity => entity.MergeRequestId == mergeRequestId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(
                context,
                entities.Select(entity => entity.UserId),
                cancellationToken
            )
            .ConfigureAwait(false);

        return entities
            .Select(entity => new MergeRequestApprovalDto
            {
                UserId = OpenGitBase.Features.Users.Contracts.Models.UserId.From(entity.UserId),
                Username = usernames.GetValueOrDefault(entity.UserId),
                CommitSha = entity.CommitSha,
                CreatedAt = entity.CreatedAt,
            })
            .OrderBy(dto => dto.CreatedAt)
            .ToList();
    }

    public IReadOnlyList<MergeRequestApprovalDto> ApprovalsAtHead(
        IReadOnlyList<MergeRequestApprovalDto> approvals,
        string sourceHeadSha
    ) =>
        approvals
            .Where(approval =>
                string.Equals(approval.CommitSha, sourceHeadSha, StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

    public async Task<MergeRequestDto> BuildDtoAsync(
        OpenGitBaseDbContext context,
        MergeRequestEntity entity,
        MergeRequestTargetPolicy targetPolicy,
        CancellationToken cancellationToken
    )
    {
        var approvals = await LoadApprovalsAsync(context, entity.Id, cancellationToken)
            .ConfigureAwait(false);
        var approvalsAtHead = ApprovalsAtHead(approvals, entity.SourceHeadSha);
        var usernames = await MergeRequestProjection
            .ResolveUsernamesAsync(context, [entity.CreatorUserId], cancellationToken)
            .ConfigureAwait(false);

        return MergeRequestProjection.ToDto(
            entity,
            usernames,
            approvals,
            targetPolicy.RequiredApprovalCount,
            approvalsAtHead.Count
        );
    }

    public async Task EvaluateAndTransitionAsync(
        OpenGitBaseDbContext context,
        MergeRequestEntity entity,
        MergeRequestTargetPolicy targetPolicy,
        CancellationToken cancellationToken
    )
    {
        if (entity.IsDraft || entity.Status is (int)MergeRequestStatus.Closed or (int)MergeRequestStatus.Merged)
        {
            return;
        }

        var approvals = await LoadApprovalsAsync(context, entity.Id, cancellationToken)
            .ConfigureAwait(false);
        var approvalsAtHead = ApprovalsAtHead(approvals, entity.SourceHeadSha);
        var gateContext = new MergeRequestGateContext
        {
            MergeRequest = ToSnapshot(entity),
            TargetPolicy = targetPolicy,
            ApprovalsAtHead = approvalsAtHead,
        };

        var gateResult = await _gateRegistry
            .EvaluateAllAsync(gateContext, cancellationToken)
            .ConfigureAwait(false);

        var status = (MergeRequestStatus)entity.Status;
        if (gateResult.IsSatisfied && status == MergeRequestStatus.Open)
        {
            entity.Status = (int)MergeRequestStatus.Approved;
            entity.UpdatedAt = _systemClock.UtcNow;
        }
        else if (!gateResult.IsSatisfied && status == MergeRequestStatus.Approved)
        {
            entity.Status = (int)MergeRequestStatus.Open;
            entity.UpdatedAt = _systemClock.UtcNow;
        }
    }

    public async Task HandleSourceHeadChangedAsync(
        OpenGitBaseDbContext context,
        MergeRequestEntity entity,
        MergeRequestTargetPolicy targetPolicy,
        string previousSourceHeadSha,
        CancellationToken cancellationToken
    )
    {
        if (
            string.Equals(
                previousSourceHeadSha,
                entity.SourceHeadSha,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return;
        }

        if (targetPolicy.DismissApprovalsOnPush)
        {
            var mergeRequestId = entity.Id;
            var approvalEntities = await context
                .Set<MergeRequestApprovalEntity>()
                .Where(approval => approval.MergeRequestId == mergeRequestId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            context.Set<MergeRequestApprovalEntity>().RemoveRange(approvalEntities);

            if (entity.Status == (int)MergeRequestStatus.Approved)
            {
                entity.Status = (int)MergeRequestStatus.Open;
            }
        }

        entity.UpdatedAt = _systemClock.UtcNow;
        await EvaluateAndTransitionAsync(context, entity, targetPolicy, cancellationToken)
            .ConfigureAwait(false);
    }

    private static MergeRequestEntitySnapshot ToSnapshot(MergeRequestEntity entity) =>
        new()
        {
            Id = entity.Id,
            RepositoryId = entity.RepositoryId,
            Number = entity.Number,
            Status = (MergeRequestStatus)entity.Status,
            IsDraft = entity.IsDraft,
            CreatorUserId = entity.CreatorUserId,
            SourceHeadSha = entity.SourceHeadSha,
            TargetRef = entity.TargetRef,
        };
}
