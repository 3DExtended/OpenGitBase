#pragma warning disable SA1402
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class CreateDiscussionQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesDiscussionWithSequentialNumber()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<CreateDiscussionQueryHandler>();

        var first = await handler.RunQueryAsync(
            new CreateDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                CreatorUserId = DiscussionTestData.CreatorUserId,
                Title = "First",
            },
            CancellationToken.None
        );

        var second = await handler.RunQueryAsync(
            new CreateDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                CreatorUserId = DiscussionTestData.CreatorUserId,
                Title = "Second",
            },
            CancellationToken.None
        );

        Assert.True(first.IsSome);
        Assert.True(second.IsSome);
        Assert.Equal(1, first.Get().Number);
        Assert.Equal(2, second.Get().Number);
        Assert.Equal(DiscussionStatus.Open, first.Get().Status);
    }

    [Fact]
    public async Task RunQueryAsync_CreatesTheDiscussionOnceWhenTheCommitAcknowledgementIsLost()
    {
        // The COMMIT lands but its acknowledgement does not; the retry must not turn the saved discussion into a 500.
        var lostCommit = new LostCommitSimulator();
        await using var scope = new DiscussionHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();
        lostCommit.LoseNextCommitInserting("discussions");

        var handler = scope.GetHandler<CreateDiscussionQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateDiscussionQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                CreatorUserId = DiscussionTestData.CreatorUserId,
                Title = "Saved once",
            },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostCommits);
        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        var stored = Assert.Single(await verifyContext.Set<DiscussionEntity>().AsNoTracking().ToListAsync());
        Assert.Equal(result.Get().Id.Value, stored.Id);
        Assert.Equal(1, result.Get().Number);
    }
}
