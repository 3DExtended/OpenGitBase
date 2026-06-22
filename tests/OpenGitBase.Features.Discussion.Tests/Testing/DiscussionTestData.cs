using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
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

    public static async Task<DiscussionEntity> SeedDiscussionAsync(
        OpenGitBaseDbContext context,
        DiscussionStatus status = DiscussionStatus.Open,
        bool hasEverBeenEngaged = false
    )
    {
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
}
