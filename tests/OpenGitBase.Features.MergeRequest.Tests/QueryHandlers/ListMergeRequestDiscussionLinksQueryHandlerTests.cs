using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class ListMergeRequestDiscussionLinksQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_UnknownMergeRequest_ReturnsEmptyList()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<ListMergeRequestDiscussionLinksQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListMergeRequestDiscussionLinksQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 99,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Empty(result.Get());
    }
}
