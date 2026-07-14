using Microsoft.EntityFrameworkCore;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;
using OpenGitBase.Features.Discussion.QueryHandlers;
using OpenGitBase.Features.Discussion.Tests.Testing;

namespace OpenGitBase.Features.Discussion.Tests.QueryHandlers;

public class CreateDiscussionLinkQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesParentLink()
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
        var created = await createHandler.RunQueryAsync(
            new CreateDiscussionLinkQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = 43,
                TargetDiscussionNumber = 42,
                RelationshipType = DiscussionRelationshipType.Parent,
            },
            CancellationToken.None);

        Assert.True(created.IsSome);
        Assert.Equal(42, created.Get().TargetDiscussionNumber);
        Assert.Equal(DiscussionRelationshipType.Parent, created.Get().RelationshipType);
        Assert.Equal("[PRD] Parent spec", created.Get().TargetDiscussionTitle);
    }

    [Fact]
    public async Task RunQueryAsync_DuplicateRelationship_ReturnsExistingWithoutSecondRow()
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
                RelationshipType = DiscussionRelationshipType.Related,
            },
            CancellationToken.None);
        await createHandler.RunQueryAsync(
            new CreateDiscussionLinkQuery
            {
                RepositoryId = DiscussionTestData.RepositoryId,
                Number = 1,
                TargetDiscussionNumber = 2,
                RelationshipType = DiscussionRelationshipType.Related,
            },
            CancellationToken.None);

        await using var verifyContext = await scope.CreateDbContextAsync();
        var count = await verifyContext.Set<DiscussionLinkEntity>().CountAsync();
        Assert.Equal(1, count);
    }
}
