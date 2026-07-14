using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class ListDiscussionLinksQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsOutgoingLinksWithMetadata()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await DiscussionTestData.SeedDiscussionAsync(
            context,
            number: 42,
            title: "[PRD] Parent spec");
        await DiscussionTestData.SeedDiscussionAsync(
            context,
            number: 43,
            title: "[slice] mr-01 — API client");

        var createHandler = scope.GetHandler<CreateDiscussionLinkQueryHandler>();
        await createHandler.RunQueryAsync(
            new CreateDiscussionLinkQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = 43,
                TargetDiscussionNumber = 42,
                RelationshipType = DiscussionRelationshipType.Parent,
            },
            CancellationToken.None);

        var listHandler = scope.GetHandler<ListDiscussionLinksQueryHandler>();
        var listed = await listHandler.RunQueryAsync(
            new ListDiscussionLinksQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = 43,
            },
            CancellationToken.None);

        Assert.True(listed.IsSome);
        var link = Assert.Single(listed.Get());
        Assert.Equal(42, link.TargetDiscussionNumber);
        Assert.Equal(DiscussionRelationshipType.Parent, link.RelationshipType);
        Assert.Equal("Open", link.TargetDiscussionStatus);
    }
}
