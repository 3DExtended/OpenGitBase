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

public class RepositoryContentControllerTests
{
    [Fact]
    public async Task GetTree_PublicRepository_ReturnsOk()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            configureStorage: ConfigureSuccessfulTree
        );

        var result = await controller.GetTree("owner", "repo", "main", string.Empty, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RepositoryContentTreeResponse>(ok.Value);
        Assert.Equal("main", response.Ref);
        Assert.Single(response.Entries);
        Assert.Equal("README.md", response.Entries[0].Name);
    }

    [Fact]
    public async Task GetTree_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var controller = CreateController(repository, authenticatedUserId: null);

        var result = await controller.GetTree("owner", "repo", "main", string.Empty, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetTree_PrivateRepositoryOutsider_ReturnsForbidden()
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

        var result = await controller.GetTree("owner", "repo", "main", string.Empty, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetTree_WhenRefNameMissing_ReturnsBadRequest()
    {
        var controller = CreateController(
            CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false),
            authenticatedUserId: null
        );

        var result = await controller.GetTree("owner", "repo", string.Empty, string.Empty, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static void ConfigureSuccessfulTree(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        RepositoryDto repository
    )
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
                                InternalHost = "replica.local",
                                InternalHttpPort = 8080,
                                IsHealthy = true,
                                IsPrimary = false,
                                IsInSync = true,
                            },
                        ],
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetStorageNodeApiTokenQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From("storage-token"));

        storageContentClient
            .GetTreeAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                repository.PhysicalPath,
                "main",
                string.Empty,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new StorageContentTreePayload
                {
                    Ref = "main",
                    Path = string.Empty,
                    Entries =
                    [
                        new StorageContentEntryPayload
                        {
                            Name = "README.md",
                            Path = "README.md",
                            Type = "blob",
                            Size = 12,
                        },
                    ],
                }
            );
    }

    private static RepositoryContentController CreateController(
        RepositoryDto repository,
        UserId? authenticatedUserId,
        Action<IQueryProcessor, IStorageContentClient, RepositoryDto>? configureStorage = null
    )
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var storageContentClient = Substitute.For<IStorageContentClient>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        configureStorage?.Invoke(queryProcessor, storageContentClient, repository);

        return CreateController(queryProcessor, authenticatedUserId, storageContentClient);
    }

    private static RepositoryContentController CreateController(
        IQueryProcessor queryProcessor,
        UserId? authenticatedUserId,
        IStorageContentClient? storageContentClient = null
    )
    {
        storageContentClient ??= Substitute.For<IStorageContentClient>();
        var cache = Substitute.For<IRepositoryContentCache>();
        cache
            .GetAsync<RepositoryContentTreeResponse>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RepositoryContentTreeResponse?)null);

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

        var controller = new RepositoryContentController(contentService)
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
