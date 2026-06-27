using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.MergeRequest;

internal static class MergeRequestProjection
{
    public static bool IsActiveStatus(MergeRequestStatus status) =>
        status is MergeRequestStatus.Draft or MergeRequestStatus.Open or MergeRequestStatus.Approved;

    public static async Task<int> AllocateNumberAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var max = await context
            .Set<MergeRequestEntity>()
            .Where(entity => entity.RepositoryId == repositoryId)
            .MaxAsync(entity => (int?)entity.Number, cancellationToken)
            .ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    public static Task<MergeRequestEntity?> FindByNumberAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        int number,
        CancellationToken cancellationToken
    ) =>
        context
            .Set<MergeRequestEntity>()
            .FirstOrDefaultAsync(
                entity => entity.RepositoryId == repositoryId && entity.Number == number,
                cancellationToken
            );

    public static async Task<bool> HasActivePairAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        string sourceRef,
        string targetRef,
        CancellationToken cancellationToken
    )
    {
        var activeStatuses = new[]
        {
            (int)MergeRequestStatus.Draft,
            (int)MergeRequestStatus.Open,
            (int)MergeRequestStatus.Approved,
        };

        return await context
            .Set<MergeRequestEntity>()
            .AnyAsync(
                entity =>
                    entity.RepositoryId == repositoryId
                    && entity.SourceRef == sourceRef
                    && entity.TargetRef == targetRef
                    && activeStatuses.Contains(entity.Status),
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveUsernamesAsync(
        OpenGitBaseDbContext context,
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken
    )
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await context
            .Set<UserEntity>()
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.Username, cancellationToken)
            .ConfigureAwait(false);
    }

    public static MergeRequestDto ToDto(
        MergeRequestEntity entity,
        IReadOnlyDictionary<Guid, string>? usernames = null,
        IReadOnlyList<MergeRequestApprovalDto>? approvals = null,
        int requiredApprovalCount = 0,
        int? approvalCountAtHead = null
    )
    {
        var approvalList = approvals ?? [];
        var atHead = approvalCountAtHead
            ?? approvalList.Count(approval =>
                string.Equals(
                    approval.CommitSha,
                    entity.SourceHeadSha,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        var dto = new MergeRequestDto
        {
            Id = MergeRequestId.From(entity.Id),
            RepositoryId = entity.RepositoryId,
            Number = entity.Number,
            Title = entity.Title,
            Body = entity.Body,
            Status = (MergeRequestStatus)entity.Status,
            IsDraft = entity.IsDraft,
            CreatorUserId = UserId.From(entity.CreatorUserId),
            SourceRef = entity.SourceRef,
            TargetRef = entity.TargetRef,
            SourceHeadSha = entity.SourceHeadSha,
            TargetBaseSha = entity.TargetBaseSha,
            MergeCommitSha = entity.MergeCommitSha,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Approvals = approvalList,
            RequiredApprovalCount = requiredApprovalCount,
            ApprovalCountAtHead = atHead,
        };

        if (usernames is not null && usernames.TryGetValue(entity.CreatorUserId, out var username))
        {
            dto.CreatorUsername = username;
        }

        return dto;
    }
}
