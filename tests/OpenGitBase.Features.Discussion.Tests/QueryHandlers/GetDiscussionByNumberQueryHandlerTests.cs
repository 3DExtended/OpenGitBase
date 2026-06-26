#pragma warning disable SA1402
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class GetDiscussionByNumberQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WithoutIncludeComments_OmitsComments()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);
        await DiscussionTestData.SeedRootCommentAsync(
            context,
            discussion.Id,
            DiscussionTestData.CreatorUserId,
            body: "Root comment"
        );

        var handler = scope.GetHandler<GetDiscussionByNumberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetDiscussionByNumberQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = discussion.Number,
                IncludeComments = false,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var dto = result.Get();
        Assert.Null(dto.Comments);
    }

    [Fact]
    public async Task RunQueryAsync_WithIncludeComments_ReturnsNestedComments()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);
        var root = await DiscussionTestData.SeedRootCommentAsync(
            context,
            discussion.Id,
            DiscussionTestData.CreatorUserId,
            body: "Root comment",
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

        var handler = scope.GetHandler<GetDiscussionByNumberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetDiscussionByNumberQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = discussion.Number,
                IncludeComments = true,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var dto = result.Get();
        Assert.NotNull(dto.Comments);
        Assert.Single(dto.Comments);
        Assert.Single(dto.Comments[0].Replies);
        Assert.Equal(1, dto.Comments[0].ReplyCount);
    }

    [Fact]
    public async Task RunQueryAsync_WithIncludeComments_ReturnsMultipleRootThreads()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);

        foreach (var body in new[] { "asd", "## sdfs", "asdf", "asdf" })
        {
            await DiscussionTestData.SeedRootCommentAsync(
                context,
                discussion.Id,
                DiscussionTestData.CreatorUserId,
                body: body
            );
        }

        var handler = scope.GetHandler<GetDiscussionByNumberQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetDiscussionByNumberQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = discussion.Number,
                IncludeComments = true,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var dto = result.Get();
        Assert.NotNull(dto.Comments);
        Assert.Equal(4, dto.Comments.Count);
        Assert.All(dto.Comments, comment => Assert.Empty(comment.Replies));
    }
}
