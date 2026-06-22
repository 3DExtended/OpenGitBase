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

    public static DiscussionCommentDto ToCommentDto(DiscussionCommentEntity entity)
    {
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
