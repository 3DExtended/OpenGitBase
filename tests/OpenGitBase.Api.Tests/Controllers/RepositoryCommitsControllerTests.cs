using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryCommitsControllerTests
{
    private const string CommitSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task GetCommit_PublicRepository_ReturnsOk()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            configureStorage: ConfigureSuccessfulCommit
        );

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RepositoryCommitResponse>(ok.Value);
        Assert.Equal(CommitSha, response.Sha);
        Assert.Equal("add feature", response.Message);
        Assert.Equal("diff", response.Kind);
        Assert.Single(response.DiffFiles);
    }

    [Fact]
    public async Task GetCommit_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetCommit_PrivateRepositoryOutsider_ReturnsForbidden()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var outsiderId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, outsiderId);

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetCommit_PrivateRepositoryMember_ReturnsOk()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var memberId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryMemberDto
                    {
                        Id = RepositoryMemberId.From(Guid.NewGuid()),
                        RepositoryId = repository.Id,
                        UserId = memberId,
                        Role = RepositoryRole.Reader,
                    }
                )
            );

        var storageContentClient = Substitute.For<IStorageContentClient>();
        ConfigureSuccessfulCommit(queryProcessor, storageContentClient, repository);
        var controller = CreateController(queryProcessor, memberId, storageContentClient);

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RepositoryCommitResponse>(ok.Value);
        Assert.Equal(CommitSha, response.Sha);
        Assert.Equal("add feature", response.Message);
    }

    [Fact]
    public async Task GetCommit_RootCommit_ReturnsRootPayload()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            configureStorage: ConfigureRootCommit
        );

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RepositoryCommitResponse>(ok.Value);
        Assert.Equal("root", response.Kind);
        Assert.Empty(response.DiffFiles);
        Assert.Single(response.RootFiles);
        Assert.Equal("README.md", response.RootFiles[0].Path);
        Assert.Equal("added", response.RootFiles[0].ChangeType);
    }

    [Fact]
    public async Task GetCommit_PrefixSha_ReturnsCanonicalSha()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var prefixSha = CommitSha[..8];
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            configureStorage: (queryProcessor, storageContentClient, repo) =>
            {
                ConfigureStorageRouting(queryProcessor);
                storageContentClient
                    .GetCommitAsync(
                        Arg.Any<RepositoryRoutingTargetDto>(),
                        Arg.Any<string>(),
                        repo.PhysicalPath,
                        prefixSha,
                        Arg.Any<CancellationToken>()
                    )
                    .Returns(
                        new StorageContentCommitDetailPayload
                        {
                            Sha = CommitSha,
                            ShortSha = prefixSha,
                            Message = "add feature",
                            AuthorName = "Test User",
                            AuthoredAt = "2026-06-01T00:00:00+00:00",
                            Kind = "diff",
                            Stats = new StorageContentCommitStatsPayload
                            {
                                FilesChanged = 1,
                                Insertions = 1,
                                Deletions = 0,
                            },
                            Files =
                            [
                                new StorageContentCommitFilePayload
                                {
                                    NewPath = "feature.txt",
                                    Status = "added",
                                },
                            ],
                        }
                    );
            }
        );

        var result = await controller.GetCommit(
            "owner",
            "repo",
            prefixSha,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RepositoryCommitResponse>(ok.Value);
        Assert.Equal(CommitSha, response.Sha);
        Assert.Equal(prefixSha, response.ShortSha);
    }

    [Fact]
    public async Task GetCommit_MessageWithSignedOffBy_RedactsEmail()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            configureStorage: (queryProcessor, storageContentClient, repo) =>
            {
                ConfigureStorageRouting(queryProcessor);
                storageContentClient
                    .GetCommitAsync(
                        Arg.Any<RepositoryRoutingTargetDto>(),
                        Arg.Any<string>(),
                        repo.PhysicalPath,
                        CommitSha,
                        Arg.Any<CancellationToken>()
                    )
                    .Returns(
                        new StorageContentCommitDetailPayload
                        {
                            Sha = CommitSha,
                            ShortSha = CommitSha[..8],
                            Message =
                                "fix bug\n\nSigned-off-by: Peter Esser <me@peter-esser.de>",
                            AuthorName = "Peter Esser",
                            AuthoredAt = "2026-06-01T00:00:00+00:00",
                            Kind = "diff",
                            Stats = new StorageContentCommitStatsPayload
                            {
                                FilesChanged = 1,
                                Insertions = 1,
                                Deletions = 0,
                            },
                            Files = [],
                        }
                    );
            }
        );

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RepositoryCommitResponse>(ok.Value);
        Assert.Equal("fix bug\n\nSigned-off-by: Peter Esser <***@***>", response.Message);
        Assert.DoesNotContain("me@peter-esser.de", response.Message);
    }

    [Fact]
    public async Task GetCommit_WhenStorageReturnsNull_ReturnsNotFound()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            configureStorage: (queryProcessor, storageContentClient, repo) =>
            {
                ConfigureStorageRouting(queryProcessor);
                storageContentClient
                    .GetCommitAsync(
                        Arg.Any<RepositoryRoutingTargetDto>(),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<CancellationToken>()
                    )
                    .Returns((StorageContentCommitDetailPayload?)null);
            }
        );

        var result = await controller.GetCommit(
            "owner",
            "repo",
            CommitSha,
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    private static void ConfigureSuccessfulCommit(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        RepositoryDto repository
    )
    {
        ConfigureStorageRouting(queryProcessor);
        storageContentClient
            .GetCommitAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                repository.PhysicalPath,
                CommitSha,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new StorageContentCommitDetailPayload
                {
                    Sha = CommitSha,
                    ShortSha = CommitSha[..8],
                    Message = "add feature",
                    AuthorName = "Test User",
                    AuthoredAt = "2026-06-01T00:00:00+00:00",
                    Kind = "diff",
                    Stats = new StorageContentCommitStatsPayload
                    {
                        FilesChanged = 1,
                        Insertions = 1,
                        Deletions = 0,
                    },
                    Files =
                    [
                        new StorageContentCommitFilePayload
                        {
                            NewPath = "feature.txt",
                            Status = "added",
                            Hunks =
                            [
                                new StorageContentDiffHunkPayload
                                {
                                    OldStart = 0,
                                    OldLines = 0,
                                    NewStart = 1,
                                    NewLines = 1,
                                    Lines =
                                    [
                                        new StorageContentDiffLinePayload
                                        {
                                            Type = "add",
                                            Content = "feature",
                                            NewLineNumber = 1,
                                        },
                                    ],
                                },
                            ],
                        },
                    ],
                }
            );
    }

    private static void ConfigureRootCommit(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        RepositoryDto repository
    )
    {
        ConfigureStorageRouting(queryProcessor);
        storageContentClient
            .GetCommitAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                repository.PhysicalPath,
                CommitSha,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new StorageContentCommitDetailPayload
                {
                    Sha = CommitSha,
                    ShortSha = CommitSha[..8],
                    Message = "init",
                    AuthorName = "Test User",
                    AuthoredAt = "2026-06-01T00:00:00+00:00",
                    Kind = "root",
                    Parents = [],
                    Stats = new StorageContentCommitStatsPayload
                    {
                        FilesChanged = 1,
                        Insertions = 0,
                        Deletions = 0,
                    },
                    Files =
                    [
                        new StorageContentCommitFilePayload
                        {
                            Path = "README.md",
                            ChangeType = "added",
                        },
                    ],
                }
            );
    }

    private static void ConfigureStorageRouting(IQueryProcessor queryProcessor)
    {
        var storageNodeId = Guid.NewGuid();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<RepositoryReplicationRoutingQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        WriteQuorumAvailable = true,
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = storageNodeId,
                                InternalHost = "storage-1",
                                InternalHttpPort = 8081,
                                IsPrimary = false,
                                IsHealthy = true,
                            },
                        ],
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("token"));
    }

    private static RepositoryCommitsController CreateController(
        RepositoryDto repository,
        UserId? authenticatedUserId,
        Action<IQueryProcessor, IStorageContentClient, RepositoryDto>? configureStorage = null
    )
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        var storageContentClient = Substitute.For<IStorageContentClient>();
        configureStorage?.Invoke(queryProcessor, storageContentClient, repository);

        return CreateController(queryProcessor, authenticatedUserId, storageContentClient);
    }

    private static RepositoryCommitsController CreateController(
        IQueryProcessor queryProcessor,
        UserId? authenticatedUserId,
        IStorageContentClient? storageContentClient = null
    )
    {
        storageContentClient ??= Substitute.For<IStorageContentClient>();
        var cache = Substitute.For<IRepositoryContentCache>();
        cache
            .GetAsync<RepositoryCommitResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RepositoryCommitResponse?)null);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(CreateHttpContext(authenticatedUserId));

        var authorization = new RepositoryContentAuthorizationService(queryProcessor, accessor);
        var contentService = new RepositoryContentService(
            authorization,
            queryProcessor,
            new WebReadReplicaSelector(),
            storageContentClient,
            cache
        );

        var controller = new RepositoryCommitsController(contentService)
        {
            ControllerContext = new ControllerContext { HttpContext = accessor.HttpContext! },
        };

        return controller;
    }

    private static void ConfigureRepositoryLookup(
        IQueryProcessor queryProcessor,
        RepositoryDto repository
    )
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
    }

    private static HttpContext CreateHttpContext(UserId? authenticatedUserId)
    {
        var context = new DefaultHttpContext();
        if (authenticatedUserId is not null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("identityproviderid", authenticatedUserId.Value.ToString())],
                    authenticationType: "Test"
                )
            );
        }

        return context;
    }

    private static RepositoryDto CreateRepository(UserId ownerId, bool isPrivate) =>
        new()
        {
            Id = RepositoryId.From(Guid.NewGuid()),
            OwnerUserId = ownerId,
            Slug = "repo",
            Name = "Repo",
            IsPrivate = isPrivate,
            PhysicalPath = "/data/repo.git",
        };
}
