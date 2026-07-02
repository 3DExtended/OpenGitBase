using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task RunQueryAsync_ParsesDistinctDiscussionReferencesFromBody()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (_, mergeRequest) = await MergeRequestTestData.SeedAsync(context);
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 3, title: "First");
        await MergeRequestTestData.SeedDiscussionAsync(context, number: 12, title: "Second");

        var handler = scope.GetHandler<SyncMergeRequestDiscussionLinksFromBodyQueryHandler>();
        var result = await handler.RunQueryAsync(
            new SyncMergeRequestDiscussionLinksFromBodyQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                Body = "Tracks #12, also see #3 and #12 again.",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var rowCount = await context
            .Set<OpenGitBase.Features.MergeRequest.Entities.MergeRequestDiscussionLinkEntity>()
            .CountAsync(link => link.MergeRequestId == mergeRequest.Id);
        Assert.Equal(2, rowCount);
    }

    [Fact]
    public async Task RunQueryAsync_IgnoresReferencesInsidePathsOrWords()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (_, mergeRequest) = await MergeRequestTestData.SeedAsync(context);

        var handler = scope.GetHandler<SyncMergeRequestDiscussionLinksFromBodyQueryHandler>();
        var result = await handler.RunQueryAsync(
            new SyncMergeRequestDiscussionLinksFromBodyQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                Body = "path/#12/footer and issue#12abc should not match",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var rowCount = await context
            .Set<OpenGitBase.Features.MergeRequest.Entities.MergeRequestDiscussionLinkEntity>()
            .CountAsync(link => link.MergeRequestId == mergeRequest.Id);
        Assert.Equal(0, rowCount);
    }
}
