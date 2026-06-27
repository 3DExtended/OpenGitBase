using NSubstitute;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class GitPushEnforcementServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WhenWriterPushesProtectedMain_DeniesDirectPush()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                            RequireMergeRequest = true,
                            AllowedPushRoles = AllowedPushRoles.None,
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto
            {
                Id = repositoryId,
                DefaultBranchName = "main",
            },
            UserId.From(Guid.NewGuid()),
            RepositoryRole.Writer,
            isRepositoryOwner: false,
            isPlatformMergeIdentity: false,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = GitShaHelper.NullSha,
                    NewSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                },
            ],
            commits: [],
            CancellationToken.None
        );

        Assert.False(result.Allowed);
        Assert.Contains("protected branch", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllowlistedAdminPushesProtectedMain_Allows()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var adminUserId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                            AllowedPushRoles = AllowedPushRoles.Admin,
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto { Id = repositoryId, DefaultBranchName = "main" },
            adminUserId,
            RepositoryRole.Admin,
            isRepositoryOwner: false,
            isPlatformMergeIdentity: false,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = GitShaHelper.NullSha,
                    NewSha = "cccccccccccccccccccccccccccccccccccccccc",
                },
            ],
            commits: [],
            CancellationToken.None
        );

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPlatformIdentityPushesProtectedMain_Allows()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto { Id = repositoryId, DefaultBranchName = "main" },
            UserId.From(Guid.Empty),
            RepositoryRole.None,
            isRepositoryOwner: false,
            isPlatformMergeIdentity: true,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    NewSha = "dddddddddddddddddddddddddddddddddddddddd",
                    IsForcePush = true,
                },
            ],
            commits: [],
            CancellationToken.None
        );

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task EvaluateAsync_WhenForbiddenPathPresent_DeniesWithRuleName()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = false,
                            RequireMergeRequest = false,
                            PushRules =
                            [
                                new PushRuleDto
                                {
                                    RuleType = PushRuleType.ForbiddenPaths,
                                    ConfigJson = """{"globs":["*.pem"]}""",
                                },
                            ],
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto { Id = repositoryId, DefaultBranchName = "main" },
            UserId.From(Guid.NewGuid()),
            RepositoryRole.Writer,
            isRepositoryOwner: true,
            isPlatformMergeIdentity: false,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = GitShaHelper.NullSha,
                    NewSha = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                },
            ],
            commits:
            [
                new GitPushCommitRequest
                {
                    Sha = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
                    Message = "add key",
                    ChangedPaths = ["secrets/private.pem"],
                },
            ],
            CancellationToken.None
        );

        Assert.False(result.Allowed);
        Assert.Contains("ForbiddenPaths", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenDcoMissing_DeniesWithRuleName()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = false,
                            RequireMergeRequest = false,
                            PushRules =
                            [
                                new PushRuleDto
                                {
                                    RuleType = PushRuleType.RequireDco,
                                    ConfigJson = """{"required":true}""",
                                },
                            ],
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto { Id = repositoryId, DefaultBranchName = "main" },
            UserId.From(Guid.NewGuid()),
            RepositoryRole.Writer,
            isRepositoryOwner: true,
            isPlatformMergeIdentity: false,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = GitShaHelper.NullSha,
                    NewSha = "ffffffffffffffffffffffffffffffffffffffff",
                },
            ],
            commits:
            [
                new GitPushCommitRequest
                {
                    Sha = "ffffffffffffffffffffffffffffffffffffffff",
                    Message = "feat: add thing",
                },
            ],
            CancellationToken.None
        );

        Assert.False(result.Allowed);
        Assert.Contains("RequireDco", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenForcePushToProtectedTargetDenied_BlocksWriter()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = false,
                            RequireMergeRequest = false,
                            AllowedPushRoles = AllowedPushRoles.Writer,
                            ForcePushPolicy = ForcePushPolicy.DenyAll,
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto { Id = repositoryId, DefaultBranchName = "main" },
            UserId.From(Guid.NewGuid()),
            RepositoryRole.Writer,
            isRepositoryOwner: false,
            isPlatformMergeIdentity: false,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    NewSha = "1111111111111111111111111111111111111111",
                    IsForcePush = true,
                },
            ],
            commits: [],
            CancellationToken.None
        );

        Assert.False(result.Allowed);
        Assert.Contains("Force-push", result.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_WhenForcePushToUnprotectedFeatureBranch_Allows()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListProtectedBranchRulesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<ProtectedBranchRuleDto>>(
                    [
                        new ProtectedBranchRuleDto
                        {
                            RepositoryId = repositoryId,
                            Pattern = "main",
                            BlockDirectPush = true,
                            ForcePushPolicy = ForcePushPolicy.DenyAll,
                        },
                    ]
                )
            );

        var service = CreateService(queryProcessor);
        var result = await service.EvaluateAsync(
            new RepositoryDto { Id = repositoryId, DefaultBranchName = "main" },
            UserId.From(Guid.NewGuid()),
            RepositoryRole.Writer,
            isRepositoryOwner: false,
            isPlatformMergeIdentity: false,
            refUpdates:
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/feature/login",
                    OldSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    NewSha = "2222222222222222222222222222222222222222",
                    IsForcePush = true,
                },
            ],
            commits: [],
            CancellationToken.None
        );

        Assert.True(result.Allowed);
    }

    [Fact]
    public async Task EnrichForcePushFlagsAsync_WhenStorageReportsNonAncestor_SetsForcePushTrue()
    {
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var storageClient = Substitute.For<IStorageContentClient>();
        var storageNodeId = Guid.NewGuid();
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
                                StorageNodeId = storageNodeId,
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
        storageClient
            .IsAncestorAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var service = CreateService(queryProcessor, storageClient);
        var enriched = await service.EnrichForcePushFlagsAsync(
            new RepositoryDto
            {
                Id = repositoryId,
                PhysicalPath = $"/srv/git/{repositoryId.Value}.git",
            },
            [
                new GitRefUpdateRequest
                {
                    RefName = "refs/heads/main",
                    OldSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    NewSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                },
            ],
            CancellationToken.None
        );

        Assert.True(enriched[0].IsForcePush);
    }

    private static GitPushEnforcementService CreateService(
        IQueryProcessor queryProcessor,
        IStorageContentClient? storageClient = null
    ) =>
        new(
            queryProcessor,
            storageClient ?? Substitute.For<IStorageContentClient>(),
            new WebReadReplicaSelector()
        );
}
