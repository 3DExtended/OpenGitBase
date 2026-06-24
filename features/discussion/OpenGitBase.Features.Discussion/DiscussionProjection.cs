using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion;

internal static class DiscussionProjection
{
    public static DiscussionDto ToDto(DiscussionEntity entity)
    {
        return new DiscussionDto
        {
            Id = DiscussionId.From(entity.Id),
            RepositoryId = entity.RepositoryId,
            Number = entity.Number,
            Title = entity.Title,
            Body = entity.Body,
            Status = (DiscussionStatus)entity.Status,
            HasEverBeenEngaged = entity.HasEverBeenEngaged,
            CreatorUserId = UserId.From(entity.CreatorUserId),
            AssigneeUserId = entity.AssigneeUserId is null
                ? null
                : UserId.From(entity.AssigneeUserId.Value),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            Tags = entity.TagAssignments
                .Select(a => new RepositoryTagDto
                {
                    Id = RepositoryTagId.From(a.Tag!.Id),
                    RepositoryId = a.Tag.RepositoryId,
                    Name = a.Tag.Name,
                    Color = a.Tag.Color,
                    CreatedAt = a.Tag.CreatedAt,
                })
                .OrderBy(t => t.Name)
                .ToList(),
        };
    }

    public static DiscussionCommentDto ToCommentDto(
        DiscussionCommentEntity entity,
        IReadOnlyList<DiscussionCommentDto>? replies = null,
        bool orphanedFromDeletedRoot = false
    )
    {
        var replyList = replies ?? [];
        return new DiscussionCommentDto
        {
            Id = DiscussionCommentId.From(entity.Id),
            DiscussionId = DiscussionId.From(entity.DiscussionId),
            AuthorUserId = UserId.From(entity.AuthorUserId),
            BodyMarkdown = entity.BodyMarkdown,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            EditedAt = entity.EditedAt,
            DeletedAt = entity.DeletedAt,
            DeletedByUserId = entity.DeletedByUserId is null
                ? null
                : UserId.From(entity.DeletedByUserId.Value),
            IsDeleted = entity.DeletedAt is not null,
            ParentCommentId = entity.ParentCommentId is null
                ? null
                : DiscussionCommentId.From(entity.ParentCommentId.Value),
            IsResolved = entity.ResolvedAt is not null,
            ResolvedAt = entity.ResolvedAt,
            ResolvedByUserId = entity.ResolvedByUserId is null
                ? null
                : UserId.From(entity.ResolvedByUserId.Value),
            ReplyCount = replyList.Count,
            LastReplyAt = replyList.Count > 0 ? replyList[^1].CreatedAt : null,
            OrphanedFromDeletedRoot = orphanedFromDeletedRoot,
            Replies = replyList,
            Anchor = entity.Anchor is null
                ? null
                : new CommentAnchorDto
                {
                    Ref = entity.Anchor.Ref,
                    CommitSha = entity.Anchor.CommitSha,
                    FilePath = entity.Anchor.FilePath,
                    Line = entity.Anchor.Line,
                    EndLine = entity.Anchor.EndLine,
                },
        };
    }

    public static IReadOnlyList<DiscussionCommentDto> BuildNestedCommentList(
        IReadOnlyList<DiscussionCommentEntity> allComments
    )
    {
        var deletedRootIds = allComments
            .Where(c => c.ParentCommentId is null && c.DeletedAt is not null)
            .Select(c => c.Id)
            .ToHashSet();

        var visible = allComments.Where(c => c.DeletedAt is null).ToList();

        var repliesByParent = visible
            .Where(c => c.ParentCommentId is not null)
            .GroupBy(c => c.ParentCommentId!.Value)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<DiscussionCommentDto>)g
                    .OrderBy(c => c.CreatedAt)
                    .Select(c => ToCommentDto(c))
                    .ToList()
            );

        var roots = visible
            .Where(c => c.ParentCommentId is null)
            .Select(c =>
                ToCommentDto(
                    c,
                    repliesByParent.GetValueOrDefault(c.Id, []),
                    orphanedFromDeletedRoot: false
                )
            )
            .ToList();

        var orphans = visible
            .Where(c =>
                c.ParentCommentId is not null && deletedRootIds.Contains(c.ParentCommentId.Value)
            )
            .Select(c => ToCommentDto(c, orphanedFromDeletedRoot: true))
            .ToList();

        return roots
            .Concat(orphans)
            .OrderBy(c => c.CreatedAt)
            .ToList();
    }

    public static IQueryable<DiscussionEntity> WithIncludes(IQueryable<DiscussionEntity> query) =>
        query
            .Include(d => d.TagAssignments)
            .ThenInclude(a => a.Tag);

    public static async Task<int> AllocateNumberAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var max = await context
            .Set<DiscussionEntity>()
            .Where(d => d.RepositoryId == repositoryId)
            .MaxAsync(d => (int?)d.Number, cancellationToken)
            .ConfigureAwait(false);
        return (max ?? 0) + 1;
    }

    public static async Task<DiscussionEntity?> FindByNumberAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        int number,
        CancellationToken cancellationToken
    )
    {
        return await WithIncludes(
                context.Set<DiscussionEntity>().Where(d =>
                    d.RepositoryId == repositoryId && d.Number == number
                )
            )
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task EnsureSubscriptionAsync(
        OpenGitBaseDbContext context,
        Guid discussionId,
        Guid userId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken
    )
    {
        var existing = await context
            .Set<DiscussionSubscriptionEntity>()
            .FirstOrDefaultAsync(
                s => s.DiscussionId == discussionId && s.UserId == userId,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.Set<DiscussionSubscriptionEntity>().Add(
                new DiscussionSubscriptionEntity
                {
                    DiscussionId = discussionId,
                    UserId = userId,
                    SubscribedAt = utcNow,
                    IsActive = true,
                }
            );
        }
        else if (!existing.IsActive)
        {
            existing.IsActive = true;
            existing.SubscribedAt = utcNow;
        }
    }
}
