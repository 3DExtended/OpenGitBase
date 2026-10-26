using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class CreateRepositoryTagQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesTheTagOnceWhenTheCommitAcknowledgementIsLost()
    {
        // The COMMIT lands but its acknowledgement does not; the retry must not report the new tag as failed.
        var lostCommit = new LostCommitSimulator();
        await using var scope = new DiscussionHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();
        lostCommit.LoseNextCommitInserting("repository_tags");

        var handler = scope.GetHandler<CreateRepositoryTagQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateRepositoryTagQuery { RepositoryId = DiscussionTestData.RepositoryId, Name = "bug", Color = "#ff0000" },
            CancellationToken.None
        );

        Assert.Equal(1, lostCommit.LostCommits);
        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        var stored = Assert.Single(await verifyContext.Set<RepositoryTagEntity>().AsNoTracking().ToListAsync());
        Assert.Equal(result.Get().Id.Value, stored.Id);
        Assert.Equal("bug", result.Get().Name);
    }
}
