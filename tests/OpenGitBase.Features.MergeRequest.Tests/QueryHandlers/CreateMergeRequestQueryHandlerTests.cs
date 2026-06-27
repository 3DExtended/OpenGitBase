using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class CreateMergeRequestQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesSequentialNumbers()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<CreateMergeRequestQueryHandler>();

        var first = await handler.RunQueryAsync(MergeRequestTestData.CreateQuery(), CancellationToken.None);
        var second = await handler.RunQueryAsync(
            new CreateMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                CreatorUserId = MergeRequestTestData.CreatorUserId,
                Title = "Second MR",
                SourceRef = "feature/other",
                TargetRef = MergeRequestTestData.TargetRef,
                SourceHeadSha = MergeRequestTestData.SourceHeadSha,
                TargetBaseSha = MergeRequestTestData.TargetBaseSha,
            },
            CancellationToken.None
        );

        Assert.True(first.IsSome);
        Assert.True(second.IsSome);
        Assert.Equal(1, first.Get().Number);
        Assert.Equal(2, second.Get().Number);
    }

    [Fact]
    public async Task RunQueryAsync_CreatesDraftWhenRequested()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<CreateMergeRequestQueryHandler>();

        var result = await handler.RunQueryAsync(
            MergeRequestTestData.CreateQuery(isDraft: true),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Draft, result.Get().Status);
        Assert.True(result.Get().IsDraft);
    }

    [Fact]
    public async Task RunQueryAsync_RejectsDuplicateActivePair()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<CreateMergeRequestQueryHandler>();

        var first = await handler.RunQueryAsync(MergeRequestTestData.CreateQuery(), CancellationToken.None);
        var duplicate = await handler.RunQueryAsync(MergeRequestTestData.CreateQuery(), CancellationToken.None);

        Assert.True(first.IsSome);
        Assert.True(duplicate.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_RejectsSameSourceAndTarget()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        var handler = scope.GetHandler<CreateMergeRequestQueryHandler>();

        var query = MergeRequestTestData.CreateQuery();
        query.SourceRef = query.TargetRef;

        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsNone);
    }
}
