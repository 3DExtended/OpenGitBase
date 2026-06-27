using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.QueryHandlers;
using OpenGitBase.Features.MergeRequest.Tests.Testing;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class DismissMergeRequestApprovalsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_DismissesApprovalsAndRevertsApprovedToOpen()
    {
        await using var scope = new MergeRequestHandlerTestScope();
        await scope.EnsureCreatedAsync();
        await using var context = await scope.CreateDbContextAsync();
        var (_, entity) = await MergeRequestTestData.SeedAsync(
            context,
            status: MergeRequestStatus.Approved
        );
        context.Set<Entities.MergeRequestApprovalEntity>().Add(
            new Entities.MergeRequestApprovalEntity
            {
                MergeRequestId = entity.Id,
                UserId = Guid.Parse("22222222-3333-4444-5555-666666666666"),
                CommitSha = entity.SourceHeadSha,
                CreatedAt = DateTimeOffset.UtcNow,
            }
        );
        await context.SaveChangesAsync();

        var handler = scope.GetHandler<DismissMergeRequestApprovalsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new DismissMergeRequestApprovalsQuery
            {
                RepositoryId = MergeRequestTestData.RepositoryId,
                Number = 1,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(MergeRequestStatus.Open, result.Get().Status);
        Assert.Empty(result.Get().Approvals);
    }
}
