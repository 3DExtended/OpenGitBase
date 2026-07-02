using Microsoft.EntityFrameworkCore;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class MergeRequestDiscussionLinkQueryHandlerIntegrationTests
{
    [Fact]
    public async Task CreateThenList_ReturnsLinkedDiscussionWithMetadata()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 4, title: "Auth refactor");

        var createHandler = scope.GetHandler<CreateMergeRequestDiscussionLinkQueryHandler>();
        var created = await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = 4,
                RelationshipType = MergeRequestRelationshipType.Implements,
            },
            CancellationToken.None
        );

        Assert.True(created.IsSome);
        Assert.Equal(4, created.Get().DiscussionNumber);
        Assert.Equal(MergeRequestRelationshipType.Implements, created.Get().RelationshipType);
        Assert.Equal("Auth refactor", created.Get().DiscussionTitle);

        var listHandler = scope.GetHandler<ListMergeRequestDiscussionLinksQueryHandler>();
        var listed = await listHandler.RunQueryAsync(
            new ListMergeRequestDiscussionLinksQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(listed.IsSome);
        var link = Assert.Single(listed.Get());
        Assert.Equal(4, link.DiscussionNumber);
        Assert.Equal("Auth refactor", link.DiscussionTitle);
        Assert.Equal("Open", link.DiscussionStatus);
    }

    [Fact]
    public async Task CreateDuplicateRelationship_ReturnsExistingWithoutSecondRow()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (_, mergeRequest) = await MergeRequestTestData.SeedAsync(context);
        var discussion = await MergeRequestTestData.SeedDiscussionAsync(context);

        var createHandler = scope.GetHandler<CreateMergeRequestDiscussionLinkQueryHandler>();
        var first = await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = discussion.Number,
                RelationshipType = MergeRequestRelationshipType.Closes,
            },
            CancellationToken.None
        );
        var second = await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = discussion.Number,
                RelationshipType = MergeRequestRelationshipType.Closes,
            },
            CancellationToken.None
        );

        Assert.True(first.IsSome);
        Assert.True(second.IsSome);
        var rowCount = await context
            .Set<MergeRequestDiscussionLinkEntity>()
            .CountAsync(link => link.MergeRequestId == mergeRequest.Id);
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task DeleteExistingLink_RemovesRow()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (_, mergeRequest) = await MergeRequestTestData.SeedAsync(context);
        var discussion = await MergeRequestTestData.SeedDiscussionAsync(context);

        var createHandler = scope.GetHandler<CreateMergeRequestDiscussionLinkQueryHandler>();
        await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = discussion.Number,
                RelationshipType = MergeRequestRelationshipType.Related,
            },
            CancellationToken.None
        );

        var deleteHandler = scope.GetHandler<DeleteMergeRequestDiscussionLinkQueryHandler>();
        var deleted = await deleteHandler.RunQueryAsync(
            new DeleteMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = discussion.Number,
                RelationshipType = MergeRequestRelationshipType.Related,
            },
            CancellationToken.None
        );

        Assert.True(deleted.IsSome);
        var rowCount = await context
            .Set<MergeRequestDiscussionLinkEntity>()
            .CountAsync(link => link.MergeRequestId == mergeRequest.Id);
        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task ListMultipleRelationshipTypes_ReturnsAllLinks()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context);
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 2, title: "Related work");
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 3, title: "Implements spec");
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 4, title: "Closes bug");

        var createHandler = scope.GetHandler<CreateMergeRequestDiscussionLinkQueryHandler>();
        await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = 2,
                RelationshipType = MergeRequestRelationshipType.Related,
            },
            CancellationToken.None
        );
        await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = 3,
                RelationshipType = MergeRequestRelationshipType.Implements,
            },
            CancellationToken.None
        );
        await createHandler.RunQueryAsync(
            new CreateMergeRequestDiscussionLinkQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                DiscussionNumber = 4,
                RelationshipType = MergeRequestRelationshipType.Closes,
            },
            CancellationToken.None
        );

        var listHandler = scope.GetHandler<ListMergeRequestDiscussionLinksQueryHandler>();
        var listed = await listHandler.RunQueryAsync(
            new ListMergeRequestDiscussionLinksQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(listed.IsSome);
        Assert.Equal(3, listed.Get().Count);
        Assert.Contains(listed.Get(), link => link.RelationshipType == MergeRequestRelationshipType.Closes);
        Assert.Contains(listed.Get(), link => link.RelationshipType == MergeRequestRelationshipType.Implements);
        Assert.Contains(listed.Get(), link => link.RelationshipType == MergeRequestRelationshipType.Related);
    }

    [Fact]
    public async Task SyncFromBody_CreatesRelatedLinksForReferencedDiscussions()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (_, mergeRequest) = await MergeRequestTestData.SeedAsync(context);
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 12, title: "From body");

        var syncHandler = scope.GetHandler<SyncMergeRequestDiscussionLinksFromBodyQueryHandler>();
        var synced = await syncHandler.RunQueryAsync(
            new SyncMergeRequestDiscussionLinksFromBodyQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                Body = "Tracks work in #12 and docs.",
            },
            CancellationToken.None
        );

        Assert.True(synced.IsSome);
        var rowCount = await context
            .Set<MergeRequestDiscussionLinkEntity>()
            .CountAsync(link => link.MergeRequestId == mergeRequest.Id);
        Assert.Equal(1, rowCount);
    }
}
