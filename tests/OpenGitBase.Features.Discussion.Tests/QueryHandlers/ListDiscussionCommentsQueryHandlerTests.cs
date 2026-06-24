#pragma warning disable SA1402
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class ListDiscussionCommentsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsNestedReplies()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);
        var root = await DiscussionTestData.SeedRootCommentAsync(
            context,
            discussion.Id,
            DiscussionTestData.CreatorUserId,
            createdAt: DateTimeOffset.UtcNow.AddMinutes(-2)
        );

        var replyEntity = new DiscussionCommentEntity
        {
            Id = Guid.NewGuid(),
            DiscussionId = discussion.Id,
            AuthorUserId = DiscussionTestData.OtherUserId.Value,
            BodyMarkdown = "Reply one",
            ParentCommentId = root.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<DiscussionCommentEntity>().Add(replyEntity);
        await context.SaveChangesAsync();

        var handler = scope.GetHandler<ListDiscussionCommentsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListDiscussionCommentsQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var roots = result.Get();
        Assert.Single(roots);
        Assert.Single(roots[0].Replies);
        Assert.Equal(1, roots[0].ReplyCount);
    }

    [Fact]
    public async Task RunQueryAsync_PromotesOrphansWhenRootDeleted()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);
        var root = await DiscussionTestData.SeedRootCommentAsync(
            context,
            discussion.Id,
            DiscussionTestData.CreatorUserId
        );
        var reply = new DiscussionCommentEntity
        {
            Id = Guid.NewGuid(),
            DiscussionId = discussion.Id,
            AuthorUserId = DiscussionTestData.OtherUserId.Value,
            BodyMarkdown = "Orphan reply",
            ParentCommentId = root.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        context.Set<DiscussionCommentEntity>().Add(reply);
        root.DeletedAt = DateTimeOffset.UtcNow;
        root.DeletedByUserId = DiscussionTestData.CreatorUserId.Value;
        await context.SaveChangesAsync();

        var handler = scope.GetHandler<ListDiscussionCommentsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListDiscussionCommentsQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var items = result.Get();
        Assert.Single(items);
        Assert.True(items[0].OrphanedFromDeletedRoot);
        Assert.Empty(items[0].Replies);
    }
}
