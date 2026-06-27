using OpenGitBase.Features.MergeRequest.Contracts;
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
}
