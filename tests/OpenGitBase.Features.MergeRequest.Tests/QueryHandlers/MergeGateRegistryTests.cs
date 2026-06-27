using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Tests.Testing;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest.Tests.QueryHandlers;

public class MergeGateRegistryTests
{
    [Fact]
    public async Task EvaluateAllAsync_SatisfiedWhenRequiredApprovalsMet()
    {
        var registry = MergeGateRegistry.CreateDefault();
        var result = await registry.EvaluateAllAsync(
            new MergeRequestGateContext
            {
                MergeRequest = new MergeRequestEntitySnapshot
                {
                    Id = Guid.NewGuid(),
                    RepositoryId = MergeRequestTestData.RepositoryId,
                    Number = 1,
                    Status = MergeRequestStatus.Open,
                    SourceHeadSha = MergeRequestTestData.SourceHeadSha,
                    TargetRef = MergeRequestTestData.TargetRef,
                    CreatorUserId = MergeRequestTestData.CreatorUserId.Value,
                },
                TargetPolicy = new MergeRequestTargetPolicy { RequiredApprovalCount = 1 },
                ApprovalsAtHead =
                [
                    new MergeRequestApprovalDto
                    {
                        UserId = UserId.From(Guid.NewGuid()),
                        CommitSha = MergeRequestTestData.SourceHeadSha,
                    },
                ],
            },
            CancellationToken.None
        );

        Assert.True(result.IsSatisfied);
    }
}
