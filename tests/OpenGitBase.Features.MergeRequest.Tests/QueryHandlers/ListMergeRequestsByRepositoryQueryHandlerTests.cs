using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class ListMergeRequestsByRepositoryQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_SortsByUpdatedAtDescending()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, number: 1, updatedAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        await MergeRequestTestData.SeedAsync(
            context,
            number: 2,
            sourceRef: "feature/b",
            targetRef: MergeRequestTestData.TargetRef,
            updatedAt: DateTimeOffset.UtcNow
        );

        var handler = scope.GetHandler<ListMergeRequestsByRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListMergeRequestsByRepositoryQuery { RepositoryId = MergeRequestTestData.RepositoryId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var list = result.Get();
        Assert.Equal(2, list.Count);
        Assert.True(list[0].UpdatedAt >= list[1].UpdatedAt);
    }

    [Fact]
    public async Task RunQueryAsync_FiltersByStatus()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, number: 1, status: MergeRequestStatus.Open);
        await MergeRequestTestData.SeedAsync(
            context,
            number: 2,
            status: MergeRequestStatus.Closed,
            sourceRef: "feature/closed",
            targetRef: MergeRequestTestData.TargetRef
        );

        var handler = scope.GetHandler<ListMergeRequestsByRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListMergeRequestsByRepositoryQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Status = MergeRequestStatus.Open,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Single(result.Get());
        Assert.Equal(MergeRequestStatus.Open, result.Get()[0].Status);
    }
}
