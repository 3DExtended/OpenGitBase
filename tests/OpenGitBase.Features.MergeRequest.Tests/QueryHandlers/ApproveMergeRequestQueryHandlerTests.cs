using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class ApproveMergeRequestQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_TransitionsToApprovedWhenRequiredMet()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedProtectedBranchRuleAsync(context, requiredApprovalCount: 1);
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Open);

        var handler = scope.GetHandler<ApproveMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ApproveMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                ApproverUserId = MergeRequestTestData.ApproverUserId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Approved, result.Get().Status);
        Assert.Single(result.Get().Approvals);
        Assert.Equal(1, result.Get().ApprovalCountAtHead);
    }

    [Fact]
    public async Task RunQueryAsync_RejectsCreatorSelfApprove()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        await MergeRequestTestData.SeedRepositoryAsync(context);
        await MergeRequestTestData.SeedAsync(context, status: MergeRequestStatus.Open);

        var handler = scope.GetHandler<ApproveMergeRequestQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ApproveMergeRequestQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
                ApproverUserId = MergeRequestTestData.CreatorUserId,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
