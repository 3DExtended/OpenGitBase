#pragma warning disable SA1402
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class CreateDiscussionCommentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesReplyUnderRoot()
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

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Reply body",
                ParentCommentId = DiscussionCommentId.From(root.Id),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal("Reply body", result.Get().BodyMarkdown);
    }

    [Fact]
    public async Task RunQueryAsync_ReplyDoesNotTriggerEngaged()
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

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Reply",
                ParentCommentId = DiscussionCommentId.From(root.Id),
            },
            CancellationToken.None
        );

        var updated = await DiscussionTestData.ReloadDiscussionAsync(scope, discussion.Id);
        Assert.Equal((int)DiscussionStatus.Open, updated.Status);
        Assert.False(updated.HasEverBeenEngaged);
    }

    [Fact]
    public async Task RunQueryAsync_TopLevelNonCreatorTriggersEngaged()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Top-level",
            },
            CancellationToken.None
        );

        var updated = await DiscussionTestData.ReloadDiscussionAsync(scope, discussion.Id);
        Assert.Equal((int)DiscussionStatus.Engaged, updated.Status);
        Assert.True(updated.HasEverBeenEngaged);
    }

    [Fact]
    public async Task RunQueryAsync_ReplyOnClosedDiscussionReopensWithoutEngaging()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(
            context,
            DiscussionStatus.Resolved,
            hasEverBeenEngaged: true
        );
        var root = await DiscussionTestData.SeedRootCommentAsync(
            context,
            discussion.Id,
            DiscussionTestData.CreatorUserId
        );

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Reopen via reply",
                ParentCommentId = DiscussionCommentId.From(root.Id),
            },
            CancellationToken.None
        );

        var updated = await DiscussionTestData.ReloadDiscussionAsync(scope, discussion.Id);
        Assert.Equal((int)DiscussionStatus.Open, updated.Status);
    }

    [Fact]
    public async Task RunQueryAsync_RejectsReplyToReply()
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

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        var reply = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "First reply",
                ParentCommentId = DiscussionCommentId.From(root.Id),
            },
            CancellationToken.None
        );

        var nested = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Reply to reply",
                ParentCommentId = reply.Get().Id,
            },
            CancellationToken.None
        );

        Assert.True(nested.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_RejectsReplyToDeletedRoot()
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
        root.DeletedAt = DateTimeOffset.UtcNow;
        root.DeletedByUserId = DiscussionTestData.CreatorUserId.Value;
        await context.SaveChangesAsync();

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Orphan attempt",
                ParentCommentId = DiscussionCommentId.From(root.Id),
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}

public class ResolveSubThreadDiscussionCommentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RootAuthorCanResolveWithoutChangingDiscussionStatus()
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

        var handler = scope.GetHandler<ResolveSubThreadDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ResolveSubThreadDiscussionCommentQuery
            {
                CommentId = DiscussionCommentId.From(root.Id),
                ActingUserId = DiscussionTestData.CreatorUserId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.True(result.Get().IsResolved);

        var updatedDiscussion = await DiscussionTestData.ReloadDiscussionAsync(scope, discussion.Id);
        Assert.Equal((int)DiscussionStatus.Open, updatedDiscussion.Status);
    }

    [Fact]
    public async Task RunQueryAsync_OtherReaderCannotResolve()
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

        var handler = scope.GetHandler<ResolveSubThreadDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ResolveSubThreadDiscussionCommentQuery
            {
                CommentId = DiscussionCommentId.From(root.Id),
                ActingUserId = DiscussionTestData.OtherUserId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}

public class UnresolveSubThreadDiscussionCommentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ClearsResolvedState()
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
        root.ResolvedAt = DateTimeOffset.UtcNow;
        root.ResolvedByUserId = DiscussionTestData.CreatorUserId.Value;
        await context.SaveChangesAsync();

        var handler = scope.GetHandler<UnresolveSubThreadDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UnresolveSubThreadDiscussionCommentQuery
            {
                CommentId = DiscussionCommentId.From(root.Id),
                ActingUserId = DiscussionTestData.CreatorUserId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.False(result.Get().IsResolved);
    }
}
