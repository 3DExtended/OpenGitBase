using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Tests.Testing;

public static class DiscussionTestData
{
    public static readonly Guid RepositoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly UserId CreatorUserId = UserId.From(
        Guid.Parse("22222222-2222-2222-2222-222222222222")
    );
    public static readonly UserId OtherUserId = UserId.From(
        Guid.Parse("33333333-3333-3333-3333-333333333333")
    );

    public static async Task SeedRepositoryAsync(OpenGitBaseDbContext context)
    {
        if (
            await context.Set<RepositoryEntity>()
                .AnyAsync(r => r.Id == RepositoryId)
                .ConfigureAwait(false)
        )
        {
            return;
        }

        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = RepositoryId,
                Name = "demo",
                Slug = "demo",
                OwnerUserId = CreatorUserId.Value,
                PhysicalPath = "/tmp/demo",
            }
        );
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async Task<DiscussionEntity> SeedDiscussionAsync(
        OpenGitBaseDbContext context,
        DiscussionStatus status = DiscussionStatus.Open,
        bool hasEverBeenEngaged = false
    )
    {
        await SeedRepositoryAsync(context).ConfigureAwait(false);
        var entity = new DiscussionEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = RepositoryId,
            Number = 1,
            Title = "Test discussion",
            Body = "Body",
            Status = (int)status,
            HasEverBeenEngaged = hasEverBeenEngaged,
            CreatorUserId = CreatorUserId.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<DiscussionEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }

    public static async Task<DiscussionCommentEntity> SeedRootCommentAsync(
        OpenGitBaseDbContext context,
        Guid discussionId,
        UserId authorUserId,
        string body = "Root comment",
        DateTimeOffset? createdAt = null
    )
    {
        var utcNow = createdAt ?? DateTimeOffset.UtcNow;
        var entity = new DiscussionCommentEntity
        {
            Id = Guid.NewGuid(),
            DiscussionId = discussionId,
            AuthorUserId = authorUserId.Value,
            BodyMarkdown = body,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };
        context.Set<DiscussionCommentEntity>().Add(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
        return entity;
    }

    public static async Task<DiscussionEntity> ReloadDiscussionAsync(
        DiscussionHandlerTestScope scope,
        Guid discussionId
    )
    {
        await using var verifyContext = await scope.CreateDbContextAsync();
        return await verifyContext
            .Set<DiscussionEntity>()
            .AsNoTracking()
            .SingleAsync(d => d.Id == discussionId)
            .ConfigureAwait(false);
    }
}
