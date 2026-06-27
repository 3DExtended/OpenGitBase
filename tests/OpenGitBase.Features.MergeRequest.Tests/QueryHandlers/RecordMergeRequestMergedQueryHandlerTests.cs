using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class RecordMergeRequestMergedQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ApprovedToMerged()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Approved);

        var handler = scope.GetHandler<RecordMergeRequestMergedQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RecordMergeRequestMergedQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                MergeCommitSha = "dddddddddddddddddddddddddddddddddddddddd",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Merged, result.Get().Status);
        Assert.Equal(
            "dddddddddddddddddddddddddddddddddddddddd",
            result.Get().MergeCommitSha
        );
    }

    [Fact]
    public async Task RunQueryAsync_OpenStatus_ReturnsNone()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Open);

        var handler = scope.GetHandler<RecordMergeRequestMergedQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RecordMergeRequestMergedQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                MergeCommitSha = "dddddddddddddddddddddddddddddddddddddddd",
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
