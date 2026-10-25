using Microsoft.EntityFrameworkCore;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class CreateMergeRequestDiscussionLinkQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_InvalidDiscussion_ReturnsNone()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);

        var handler = scope.GetHandler<CreateMergeRequestDiscussionLinkQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = 999,
                RelationshipType = MergeRequestRelationshipType.Closes,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_CreatesTheLinkOnceWhenTheCommitAcknowledgementIsLost()
    {
        // The COMMIT lands but its acknowledgement does not; the retry must not report the new link as failed.
        var lostCommit = new LostCommitSimulator();
        await using var scope = new MergeRequestHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);
        var discussion = await MergeRequestTestData.SeedDiscussionAsync(context);
        lostCommit.LoseNextCommitInserting("merge_request_discussion_links");

        var handler = scope.GetHandler<CreateMergeRequestDiscussionLinkQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = discussion.Number,
                RelationshipType = MergeRequestRelationshipType.Closes,
            },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostCommits);
        Assert.True(result.IsSome);
        Assert.Equal(discussion.Number, result.Get().DiscussionNumber);
        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.Single(await verifyContext.Set<MergeRequestDiscussionLinkEntity>().AsNoTracking().ToListAsync());
    }
}
