using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

using OpenGitBase.Features.Users.Entities;

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

    public static async Task SeedUsersAsync(OpenGitBaseDbContext context)
    {
        foreach (var (userId, username) in new[]
        {
            (CreatorUserId.Value, "creator-user"),
            (OtherUserId.Value, "other-user"),
        })
        {
            if (
                await context.Set<UserEntity>()
                    .AnyAsync(user => user.Id == userId)
                    .ConfigureAwait(false)
            )
            {
                continue;
            }

            context.Set<UserEntity>().Add(
                new UserEntity
                {
                    Id = userId,
                    Username = username,
                    NormalizedUsername = username.ToUpperInvariant(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public static async Task SeedRepositoryAsync(OpenGitBaseDbContext context)
    {
        await SeedUsersAsync(context).ConfigureAwait(false);
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

    public static Task<DiscussionEntity> SeedDiscussionAsync(
        OpenGitBaseDbContext context,
        DiscussionStatus status = DiscussionStatus.Open,
        bool hasEverBeenEngaged = false) =>
        SeedDiscussionAsync(
            context,
            number: 1,
            title: "Test discussion",
            body: "Body",
            status,
            hasEverBeenEngaged);

    public static async Task<DiscussionEntity> SeedDiscussionAsync(
        OpenGitBaseDbContext context,
        int number,
        string title,
        string? body = "Body",
        DiscussionStatus status = DiscussionStatus.Open,
        bool hasEverBeenEngaged = false
    )
    {
        await SeedRepositoryAsync(context).ConfigureAwait(false);
        var entity = new DiscussionEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = RepositoryId,
            Number = number,
            Title = title,
            Body = body,
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
