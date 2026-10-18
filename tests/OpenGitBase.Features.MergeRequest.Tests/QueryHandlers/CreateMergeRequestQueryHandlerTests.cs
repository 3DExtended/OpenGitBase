using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;
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
    public async Task RunQueryAsync_CreatesTheMergeRequestOnceWhenTheCommitAcknowledgementIsLost()
    {
        // The COMMIT lands but its acknowledgement does not; the retry must not report the new merge request as failed.
        var lostCommit = new LostCommitSimulator();
        await using var scope = new MergeRequestHandlerTestScope(lostCommit.Configure);
        await scope.EnsureCreatedAsync();
        lostCommit.LoseNextCommitInserting("merge_requests");

        var handler = scope.GetHandler<CreateMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(MergeRequestTestData.CreateQuery(), CancellationToken.None);

        Assert.Equal(1, lostCommit.LostCommits);
        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        var stored = Assert.Single(await verifyContext.Set<MergeRequestEntity>().AsNoTracking().ToListAsync());
        Assert.Equal(result.Get().Id.Value, stored.Id);
        Assert.Equal(1, result.Get().Number);
    }

    [Fact]
    public async Task RunQueryAsync_ReturnsTheCreatedMergeRequestWhenLinkingItsDiscussionsFails()
    {
        // Links from the body are made after the merge request commits; failing there must not report it as failed.
        await using var scope = new MergeRequestHandlerTestScope(options =>
            options.AddInterceptors(new FailingCommandInterceptor("INSERT INTO \"merge_request_discussion_links\""))
        );
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var discussion = await MergeRequestTestData.SeedDiscussionAsync(context);
        var query = MergeRequestTestData.CreateQuery();
        query.Body = $"Implements #{discussion.Number}";

        var handler = scope.GetHandler<CreateMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(query, CancellationToken.None);

        Assert.True(result.IsSome);
        await using var verifyContext = await scope.CreateDbContextAsync();
        Assert.True(await verifyContext.Set<MergeRequestEntity>().AnyAsync(mr => mr.Id == result.Get().Id.Value));
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
