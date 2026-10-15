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
    private const string LfsOid = "4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393";

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

    [Fact]
    public async Task GetBlob_OfAnLfsPointer_DescribesTheRecordedLfsObject()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var lfs = Substitute.For<ILfsContentReader>();
        lfs.FindRecordedSizeAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns(4096L);
        var controller = CreateController(repository, authenticatedUserId: null, ConfigureLfsPointerBlob, lfs);

        var result = await controller.GetBlob("owner", "repo", "main", "assets/logo.png", CancellationToken.None);

        var response = Assert.IsType<RepositoryContentBlobResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.NotNull(response.Lfs);
        Assert.Equal(LfsOid, response.Lfs.Oid);
        Assert.Equal(4096, response.Lfs.Size);
        Assert.True(response.Lfs.Available);
        Assert.Null(response.TextContent);
        Assert.Null(response.ContentBase64);
    }

    [Fact]
    public async Task GetBlob_OfAnLfsPointerNeverUploaded_IsNotAvailable()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var lfs = Substitute.For<ILfsContentReader>();
        lfs.FindRecordedSizeAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns((long?)null);
        var controller = CreateController(repository, authenticatedUserId: null, ConfigureLfsPointerBlob, lfs);

        var result = await controller.GetBlob("owner", "repo", "main", "assets/logo.png", CancellationToken.None);

        var response = Assert.IsType<RepositoryContentBlobResponse>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.False(response.Lfs?.Available);
    }

    [Fact]
    public async Task GetRawBlob_OfAnLfsPointer_StreamsTheObjectWithItsTypeAndLength()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var content = new MemoryStream(new byte[4096]);
        var lfs = Substitute.For<ILfsContentReader>();
        lfs.FindRecordedSizeAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns(4096L);
        lfs.OpenAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns(content);
        var controller = CreateController(repository, authenticatedUserId: null, ConfigureLfsPointerRaw, lfs);

        var result = await controller.GetRawBlob("owner", "repo", "main", "assets/logo.png", CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Same(content, file.FileStream);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal("logo.png", file.FileDownloadName);
        Assert.Equal(4096, controller.Response.ContentLength);
        Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions.ToString());
    }

    [Theory]
    [InlineData("assets/page.html")]
    [InlineData("assets/drawing.svg")]
    [InlineData("assets/archive.zip")]
    public async Task GetRawBlob_OfAnLfsFileThatIsNotAPlainImage_IsServedAsOctetStream(string path)
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var lfs = Substitute.For<ILfsContentReader>();
        lfs.FindRecordedSizeAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns(4096L);
        lfs.OpenAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns(new MemoryStream(new byte[4096]));
        var controller = CreateController(repository, authenticatedUserId: null, ConfigureLfsPointerRaw, lfs);

        var result = await controller.GetRawBlob("owner", "repo", "main", path, CancellationToken.None);

        Assert.Equal("application/octet-stream", Assert.IsType<FileStreamResult>(result).ContentType);
    }

    [Fact]
    public async Task GetRawBlob_OfAnLfsPointerNeverUploaded_AnswersNotAvailable()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var lfs = Substitute.For<ILfsContentReader>();
        lfs.FindRecordedSizeAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns((long?)null);
        var controller = CreateController(repository, authenticatedUserId: null, ConfigureLfsPointerRaw, lfs);

        var result = await controller.GetRawBlob("owner", "repo", "main", "assets/logo.png", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        await lfs.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetRawBlob_OfARecordedLfsObjectNoNodeHolds_AnswersServiceUnavailableWithRetryAfter()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var lfs = Substitute.For<ILfsContentReader>();
        lfs.FindRecordedSizeAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns(4096L);
        lfs.OpenAsync(repository.Id.Value, LfsOid, Arg.Any<CancellationToken>()).Returns((Stream?)null);
        var controller = CreateController(repository, authenticatedUserId: null, ConfigureLfsPointerRaw, lfs);

        var result = await controller.GetRawBlob("owner", "repo", "main", "assets/logo.png", CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal("10", controller.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task GetRawBlob_OfAnLfsFileInAPrivateRepository_IsForbiddenForAnOutsider()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);
        var storage = Substitute.For<IStorageContentClient>();
        ConfigureLfsPointerRaw(queryProcessor, storage, repository);
        var lfs = Substitute.For<ILfsContentReader>();
        var controller = CreateController(queryProcessor, UserId.From(Guid.NewGuid()), storage, lfs);

        var result = await controller.GetRawBlob("owner", "repo", "main", "assets/logo.png", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        await lfs.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await storage.DidNotReceive().GetRawAsync(
            Arg.Any<RepositoryRoutingTargetDto>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetRawBlob_OfAnOrdinaryFile_StillReturnsItsBytes()
    {
        var repository = CreateRepository(UserId.From(Guid.NewGuid()), isPrivate: false);
        var lfs = Substitute.For<ILfsContentReader>();
        var controller = CreateController(
            repository,
            authenticatedUserId: null,
            (queryProcessor, storage, repo) =>
            {
                ConfigureRouting(queryProcessor);
                storage
                    .GetRawAsync(Arg.Any<RepositoryRoutingTargetDto>(), Arg.Any<string>(), repo.PhysicalPath, "main", "README.md", Arg.Any<CancellationToken>())
                    .Returns(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent("# Hi\n"u8.ToArray()) });
            },
            lfs
        );

        var result = await controller.GetRawBlob("owner", "repo", "main", "README.md", CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("# Hi\n"u8.ToArray(), file.FileContents);
        await lfs.DidNotReceive().FindRecordedSizeAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static void ConfigureRouting(IQueryProcessor queryProcessor)
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<RepositoryReplicationRoutingQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto
                    {
                        WriteQuorumAvailable = true,
                        Targets =
                        [
                            new RepositoryRoutingTargetDto
                            {
                                StorageNodeId = Guid.NewGuid(),
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
    }

    private static void ConfigureLfsPointerBlob(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        RepositoryDto repository
    )
    {
        ConfigureRouting(queryProcessor);
        storageContentClient
            .GetBlobAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                repository.PhysicalPath,
                "main",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new StorageContentBlobPayload
                {
                    Ref = "main",
                    Path = "assets/logo.png",
                    Size = 4096,
                    IsBinary = true,
                    PreviewKind = "image",
                    Lfs = new StorageContentLfsPointerPayload { Oid = LfsOid, Size = 4096 },
                }
            );
    }

    private static void ConfigureLfsPointerRaw(
        IQueryProcessor queryProcessor,
        IStorageContentClient storageContentClient,
        RepositoryDto repository
    )
    {
        ConfigureRouting(queryProcessor);
        storageContentClient
            .GetRawAsync(
                Arg.Any<RepositoryRoutingTargetDto>(),
                Arg.Any<string>(),
                repository.PhysicalPath,
                "main",
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(
                        System.Text.Encoding.ASCII.GetBytes($"version https://git-lfs.github.com/spec/v1\noid sha256:{LfsOid}\nsize 4096\n")
                    ),
                };
                response.Headers.Add("X-OpenGitBase-Lfs-Oid", LfsOid);
                response.Headers.Add("X-OpenGitBase-Lfs-Size", "4096");
                return response;
            });
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
        Action<IQueryProcessor, IStorageContentClient, RepositoryDto>? configureStorage = null,
        ILfsContentReader? lfsContent = null
    )
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var storageContentClient = Substitute.For<IStorageContentClient>();
        ConfigureRepositoryLookup(queryProcessor, repository);
        configureStorage?.Invoke(queryProcessor, storageContentClient, repository);

        return CreateController(queryProcessor, authenticatedUserId, storageContentClient, lfsContent);
    }

    private static RepositoryContentController CreateController(
        IQueryProcessor queryProcessor,
        UserId? authenticatedUserId,
        IStorageContentClient? storageContentClient = null,
        ILfsContentReader? lfsContent = null
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
            cache,
            lfsContent ?? Substitute.For<ILfsContentReader>()
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
