using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class ResolveDiscussionQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_OpenDiscussion_TransitionsToResolved()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(
            context,
            DiscussionStatus.Open
        );

        var handler = scope.GetHandler<ResolveDiscussionQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ResolveDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = discussion.Number,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(DiscussionStatus.Resolved, result.Get().Status);
    }

    [Fact]
    public async Task RunQueryAsync_EngagedDiscussion_TransitionsToResolved()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(
            context,
            DiscussionStatus.Engaged,
            hasEverBeenEngaged: true
        );

        var handler = scope.GetHandler<ResolveDiscussionQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ResolveDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = discussion.Number,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(DiscussionStatus.Resolved, result.Get().Status);
    }

    [Fact]
    public async Task RunQueryAsync_AlreadyResolved_ReturnsNone()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await DiscussionTestData.SeedDiscussionAsync(
            context,
            DiscussionStatus.Resolved,
            hasEverBeenEngaged: true
        );

        var handler = scope.GetHandler<ResolveDiscussionQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ResolveDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = discussion.Number,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
