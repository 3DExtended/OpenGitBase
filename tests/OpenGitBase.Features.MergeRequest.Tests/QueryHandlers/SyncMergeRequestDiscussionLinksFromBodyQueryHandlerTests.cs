using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class SyncMergeRequestDiscussionLinksFromBodyQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_NoReferences_ReturnsUnit()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<SyncMergeRequestDiscussionLinksFromBodyQueryHandler>();
        var result = await handler.RunQueryAsync(
            new SyncMergeRequestDiscussionLinksFromBodyQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                Body = "No references here.",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
    }
}
