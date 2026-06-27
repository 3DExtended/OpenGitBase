using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class DeleteMergeRequestDiscussionLinkQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_MissingLink_ReturnsNone()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);

        var handler = scope.GetHandler<DeleteMergeRequestDiscussionLinkQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DeleteMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = 1,
                RelationshipType = MergeRequestRelationshipType.Closes,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
