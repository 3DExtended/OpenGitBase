using Microsoft.AspNetCore.Http;
using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

#pragma warning disable SA1402 // File may only contain a single type

public class MergeRequestMergeServiceTests
{
    [Fact]
    public async Task MergeAsync_RejectsWhenNotApproved()
    {
        var service = CreateService(out _);
        var repository = CreateRepository();
        var mergeRequest = CreateMergeRequest(MergeRequestStatus.Open);

        var result = await service.MergeAsync(
            repository,
            mergeRequest,
            RepositoryRole.Writer,
            MergeRequestMergeStrategyDto.MergeCommit,
            deleteSourceBranch: false,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task MergeAsync_RejectsWhenMergeabilityConflicts()
    {
        var service = CreateService(out var storageClient);
        storageClient
            .CheckMergeabilityAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new StorageContentMergeabilityPayload { Status = "conflicts" });

        var repository = CreateRepository();
        var mergeRequest = CreateMergeRequest(MergeRequestStatus.Approved);

        var result = await service.MergeAsync(
            repository,
            mergeRequest,
            RepositoryRole.Writer,
            MergeRequestMergeStrategyDto.MergeCommit,
            deleteSourceBranch: false,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    private static MergeRequestMergeService CreateService(out IStorageContentClient storageClient)
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        storageClient = Substitute.For<IStorageContentClient>();
        var refService = new MergeRequestRefService(
            queryProcessor,
            storageClient,
            new WebReadReplicaSelector()
        );

        storageClient
            .ResolveRefAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
            {
                var refName = callInfo.ArgAt<string>(3);
                var sha = refName.Contains("main", StringComparison.OrdinalIgnoreCase)
                    ? "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    : "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                return new StorageContentResolveRefPayload { CommitSha = sha };
            });

        queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = Guid.NewGuid(),
                                InternalHost = "127.0.0.1",
                                InternalHttpPort = 8081,
                                IsPrimary = true,
                                IsHealthy = true,
                            },
                        ],
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("token"));
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<ProtectedBranchRuleDto>>([]));

        return new MergeRequestMergeService(
            queryProcessor,
            storageClient,
            refService,
            new PlatformMergeIdentityOptions { AccessToken = "platform-token" }
        );
    }

    private static RepositoryDto CreateRepository() =>
        new()
        {
            Id = RepositoryId.From(Guid.NewGuid()),
            PhysicalPath = "/srv/git/test.git",
        };

    private static MergeRequestDto CreateMergeRequest(MergeRequestStatus status) =>
        new()
        {
            Id = MergeRequestId.From(Guid.NewGuid()),
            Number = 1,
            Title = "Test MR",
            Status = status,
            SourceRef = "feature/a",
            TargetRef = "main",
            SourceHeadSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            TargetBaseSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
        };
}

public class MergeRequestMergeStrategyHelperTests
{
    [Fact]
    public void ResolveStrategy_RejectsWhenLockedStrategyDiffers()
    {
        var result = MergeRequestMergeStrategyHelper.ResolveStrategy(
            MergeRequestMergeStrategyDto.Squash,
            LockedMergeStrategy.MergeCommit
        );

        Assert.Null(result);
    }
}
