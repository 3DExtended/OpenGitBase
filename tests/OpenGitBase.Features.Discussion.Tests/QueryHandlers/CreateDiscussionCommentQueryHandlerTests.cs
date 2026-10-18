#pragma warning disable SA1402
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    [Fact]
    public async Task RunQueryAsync_SavesTheCommentOnceWhenTheCommitAcknowledgementIsLost()
    {
        // Prod, 2026-10-18: a 14k-character spec part committed, but the COMMIT outlived the command timeout, the
        // retrying execution strategy re-ran the INSERT, and its duplicate key turned the saved comment into a 500.
        var lostCommit = new LostCommitAcknowledgementInterceptor();
        await using var scope = new DiscussionHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);
        var body = string.Join("\n\n", Enumerable.Range(1, 16).Select(index => $"## Section {index}\n\n{new string('x', 870)}"));
        lostCommit.LoseNextCommentCommit();

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.CreatorUserId,
                BodyMarkdown = body,
            },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostAcknowledgements);
        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        var stored = Assert.Single(
            await verifyContext
                .Set<DiscussionCommentEntity>()
                .AsNoTracking()
                .Where(c => c.DiscussionId == discussion.Id)
                .ToListAsync()
        );
        Assert.Equal(result.Get().Id.Value, stored.Id);
        Assert.Equal(body, stored.BodyMarkdown);
    }

    [Fact]
    public async Task RunQueryAsync_NotifiesReopenOnceWhenTheReopeningCommitAcknowledgementIsLost()
    {
        // The retry sees the discussion its lost attempt already reopened; the subscribers still hear of the reopen.
        var lostCommit = new LostCommitAcknowledgementInterceptor();
        await using var scope = new DiscussionHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(
            context,
            DiscussionStatus.Resolved,
            hasEverBeenEngaged: true
        );
        context.Set<DiscussionSubscriptionEntity>().Add(
            new DiscussionSubscriptionEntity
            {
                DiscussionId = discussion.Id,
                UserId = DiscussionTestData.CreatorUserId.Value,
                SubscribedAt = DateTimeOffset.UtcNow,
                IsActive = true,
            }
        );
        await context.SaveChangesAsync();
        lostCommit.LoseNextCommentCommit();

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Not fixed yet",
            },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostAcknowledgements);
        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        var creatorNotifications = await verifyContext
            .Set<UserNotificationEntity>()
            .AsNoTracking()
            .Where(n => n.UserId == DiscussionTestData.CreatorUserId.Value)
            .Select(n => n.EventType)
            .ToListAsync();
        Assert.Equal(
            [(int)NotificationEventType.NewComment, (int)NotificationEventType.Reopened],
            creatorNotifications.Order()
        );
    }

    [Fact]
    public async Task RunQueryAsync_ReturnsTheSavedCommentWhenNotifyingSubscribersFails()
    {
        // Notifications go out after the comment commits; a failure there must not report the saved comment as failed.
        await using var scope = new DiscussionHandlerTestScope(options =>
            options.AddInterceptors(new FailCommandsInterceptor("INSERT INTO \"user_notifications\""))
        );
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(context);
        context.Set<DiscussionSubscriptionEntity>().Add(
            new DiscussionSubscriptionEntity
            {
                DiscussionId = discussion.Id,
                UserId = DiscussionTestData.CreatorUserId.Value,
                SubscribedAt = DateTimeOffset.UtcNow,
                IsActive = true,
            }
        );
        await context.SaveChangesAsync();

        var handler = scope.GetHandler<CreateDiscussionCommentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateDiscussionCommentQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                DiscussionNumber = discussion.Number,
                AuthorUserId = DiscussionTestData.OtherUserId,
                BodyMarkdown = "Saved even if nobody hears of it",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.True(
            await verifyContext
                .Set<DiscussionCommentEntity>()
                .AnyAsync(c => c.Id == result.Get().Id.Value)
        );
    }

    private sealed class FailCommandsInterceptor : DbCommandInterceptor
    {
        private readonly string _sqlFragment;

        public FailCommandsInterceptor(string sqlFragment)
        {
            _sqlFragment = sqlFragment;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfMatched(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            ThrowIfMatched(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        private void ThrowIfMatched(DbCommand command)
        {
            if (command.CommandText.Contains(_sqlFragment, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Simulated failure of: {_sqlFragment}");
            }
        }
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
