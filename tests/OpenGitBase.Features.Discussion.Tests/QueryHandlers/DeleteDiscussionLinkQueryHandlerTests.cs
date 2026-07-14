using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class DeleteDiscussionLinkQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RemovesExistingLink()
    {
        await using var scope = new DiscussionHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await DiscussionTestData.SeedDiscussionAsync(context, number: 1, title: "Source");
        await DiscussionTestData.SeedDiscussionAsync(context, number: 2, title: "Target");

        var createHandler = scope.GetHandler<CreateDiscussionLinkQueryHandler>();
        await createHandler.RunQueryAsync(
            new CreateDiscussionLinkQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = 1,
                TargetDiscussionNumber = 2,
                RelationshipType = DiscussionRelationshipType.Blocks,
            },
            CancellationToken.None);

        var deleteHandler = scope.GetHandler<DeleteDiscussionLinkQueryHandler>();
        var deleted = await deleteHandler.RunQueryAsync(
            new DeleteDiscussionLinkQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = 1,
                TargetDiscussionNumber = 2,
                RelationshipType = DiscussionRelationshipType.Blocks,
            },
            CancellationToken.None);

        Assert.True(deleted.IsSome);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var count = await verifyContext.Set<DiscussionLinkEntity>().CountAsync();
        Assert.Equal(0, count);
    }
}
