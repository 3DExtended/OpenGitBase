using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryControllerTests
{
    private const string DefaultRepositoryName = "My Repository";

    [Fact]
    public async Task Create_WhenUserNotFound_ReturnsUnauthorized()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<User>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(
            "my-repo",
            CreateRequest(false),
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad_slug")]
    [InlineData("has space")]
    [InlineData("special!")]
    public async Task Create_WhenSlugInvalid_ReturnsBadRequest(string slug)
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(slug, CreateRequest(false), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_WhenSlugAlreadyTaken_ReturnsBadRequest()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryBySlugForUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = RepositoryId.From(Guid.NewGuid()),
                        Slug = "my-repo",
                        OwnerUserId = userId,
                        Name = "Existing",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(
            "my-repo",
            CreateRequest(false),
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_WhenEmailNotVerified_ReturnsForbidden()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetEmailVerifiedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(false));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(
            "my-repo",
            CreateRequest(false),
            CancellationToken.None
        );

        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Create_WhenOrganizationMember_ReturnsCreatedWithOrgOwner()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = organizationId,
                        Name = "Acme",
                        Slug = "acme",
                    }
                )
            );
        organizationAccess
            .CheckMemberAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(
                new OrganizationMemberAccessCheck(
                    true,
                    true,
                    false,
                    new OrganizationDto { Id = organizationId, Name = "Acme", Slug = "acme" }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryBySlugForUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateRepositoryWithStorageQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    CreateRepositoryWithStorageResult.Created(repositoryId)
                )
            );

        var controller = CreateController(queryProcessor, userId, organizationAccess: organizationAccess);

        var result = await controller.Create(
            "team-repo",
            new CreateRepositoryRequest("Team Repo", false, "acme"),
            CancellationToken.None
        );

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var returnedId = Assert.IsType<RepositoryId>(created.Value);
        Assert.Equal(repositoryId, returnedId);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<CreateRepositoryWithStorageQuery>(query =>
                    query.ModelToCreate.OwnerUserId == UserId.From(organizationId.Value)
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Create_WhenNotOrganizationMember_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = organizationId,
                        Name = "Acme",
                        Slug = "acme",
                    }
                )
            );
        organizationAccess
            .CheckMemberAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationMemberAccessCheck(true, false, false, null));

        var controller = CreateController(queryProcessor, userId, organizationAccess: organizationAccess);

        var result = await controller.Create(
            "team-repo",
            new CreateRepositoryRequest("Team Repo", false, "acme"),
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Create_WhenSuccessful_ReturnsCreatedWithId()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryBySlugForUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateRepositoryWithStorageQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    CreateRepositoryWithStorageResult.Created(repositoryId)
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(
            "my-repo",
            CreateRequest(true),
            CancellationToken.None
        );

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(RepositoryController.Get), created.ActionName);
        var returnedId = Assert.IsType<RepositoryId>(created.Value);
        Assert.Equal(repositoryId, returnedId);

        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<CreateRepositoryWithStorageQuery>(query =>
                    query.ModelToCreate.Slug == "my-repo"
                    && query.ModelToCreate.Name == DefaultRepositoryName
                    && query.ModelToCreate.IsPrivate
                    && query.ModelToCreate.OwnerUserId == userId
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Create_WhenQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        ConfigureUserLookup(queryProcessor, userId);
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryBySlugForUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateRepositoryWithStorageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<CreateRepositoryWithStorageResult>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Create(
            "my-repo",
            CreateRequest(false),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Get_WhenFound_ReturnsOkWithDto()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var dto = new RepositoryDto
        {
            Id = repositoryId,
            OwnerUserId = userId,
            Slug = "my-repo",
            Name = DefaultRepositoryName,
            IsPrivate = false,
        };
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(dto));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Get(repositoryId.Value, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<RepositoryDto>(ok.Value);
        Assert.Equal(repositoryId, returned.Id);
        Assert.Equal(DefaultRepositoryName, returned.Name);
    }

    [Fact]
    public async Task Get_WhenMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByOwnerSlug_WhenFound_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "hello-world",
                        Name = DefaultRepositoryName,
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.GetByOwnerSlug("demo-user", "hello-world", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<RepositoryDto>(ok.Value);
        Assert.Equal(repositoryId, returned.Id);
    }

    [Fact]
    public async Task GetByOwnerSlug_WhenMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.GetByOwnerSlug("demo-user", "missing", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetUsage_WhenFound_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = RepositoryId.From(repositoryId),
                        Name = "Repo",
                        Slug = "repo",
                        OwnerUserId = userId,
                        PhysicalPath = "/srv/git/test.git",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryUsageDto
                    {
                        BytesUsed = 2048,
                        BytesLimit = 1_000_000,
                        FileSizeLimit = 100_000,
                    }
                )
            );

        var repositoryDiskUsageProvider = Substitute.For<IRepositoryDiskUsageProvider>();
        repositoryDiskUsageProvider
            .GetDiskUsageBytesAsync(Arg.Any<RepositoryDto>(), Arg.Any<CancellationToken>())
            .Returns((long?)null);

        var controller = CreateController(queryProcessor, userId, repositoryDiskUsageProvider: repositoryDiskUsageProvider);

        var result = await controller.GetUsage(repositoryId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<RepositoryUsageDto>(ok.Value);
        Assert.Equal(2048, returned.BytesUsed);
    }

    [Fact]
    public async Task GetUsage_WhenStorageReportsLiveUsage_ReturnsLiveBytes()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = RepositoryId.From(repositoryId),
                        Name = "Repo",
                        Slug = "repo",
                        OwnerUserId = userId,
                        PhysicalPath = "/srv/git/test.git",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryUsageQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryUsageDto
                    {
                        BytesUsed = 0,
                        BytesLimit = 1_000_000,
                        FileSizeLimit = 100_000,
                    }
                )
            );

        var repositoryDiskUsageProvider = Substitute.For<IRepositoryDiskUsageProvider>();
        repositoryDiskUsageProvider
            .GetDiskUsageBytesAsync(Arg.Any<RepositoryDto>(), Arg.Any<CancellationToken>())
            .Returns(26_397L);

        var controller = CreateController(queryProcessor, userId, repositoryDiskUsageProvider: repositoryDiskUsageProvider);

        var result = await controller.GetUsage(repositoryId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<RepositoryUsageDto>(ok.Value);
        Assert.Equal(26_397, returned.BytesUsed);
    }

    [Fact]
    public async Task GetUsage_WhenMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.GetUsage(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_WhenFound_ReturnsOkWithRepositories()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositories = new List<RepositoryDto>
        {
            new()
            {
                Id = RepositoryId.From(Guid.NewGuid()),
                OwnerUserId = userId,
                Slug = "repo-one",
                Name = "Repo One",
            },
        };
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Is<ListRepositoriesForUserQuery>(query => query.UserId == userId),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From((IReadOnlyList<RepositoryDto>)repositories));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<RepositoryDto>>(ok.Value);
        Assert.Single(returned);
        Assert.Equal("repo-one", returned[0].Slug);
    }

    [Fact]
    public async Task List_WhenQueryReturnsNone_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Is<ListRepositoriesForUserQuery>(query => query.UserId == userId),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option<IReadOnlyList<RepositoryDto>>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.List(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task List_WhenEmpty_ReturnsOkWithEmptyList()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Is<ListRepositoriesForUserQuery>(query => query.UserId == userId),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From((IReadOnlyList<RepositoryDto>)Array.Empty<RepositoryDto>()));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<RepositoryDto[]>(ok.Value);
        Assert.Empty(returned);
    }

    [Fact]
    public async Task UpdateMetadata_WhenMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMetadata(
            Guid.NewGuid(),
            CreateUpdateRequest("Updated"),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateMetadata_WhenOwnedByOtherUser_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var otherUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = otherUserId,
                        Slug = "protected",
                        Name = "Protected",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMetadata(
            repositoryId.Value,
            CreateUpdateRequest("Updated"),
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateMetadata_WhenOwned_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "my-repo",
                        Name = DefaultRepositoryName,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(Unit.Value));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMetadata(
            repositoryId.Value,
            CreateUpdateRequest("Updated name", isPrivate: true),
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);

        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<UpdateRepositoryQuery>(query =>
                    query.UpdatedModel.Id == repositoryId
                    && query.UpdatedModel.Name == "Updated name"
                    && query.UpdatedModel.IsPrivate
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task UpdateMetadata_WhenUpdateQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "my-repo",
                        Name = DefaultRepositoryName,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<Unit>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMetadata(
            repositoryId.Value,
            CreateUpdateRequest("Updated name"),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenOwnedByOtherUser_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var otherUserId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = otherUserId,
                        Slug = "protected",
                        Name = "Protected",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(repositoryId.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Delete_WhenOwned_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "my-repo",
                        Name = DefaultRepositoryName,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteRepositoryWithStorageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(DeleteRepositoryWithStorageResult.Deleted()));

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(repositoryId.Value, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenDeleteQueryFails_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "my-repo",
                        Name = DefaultRepositoryName,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteRepositoryWithStorageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<DeleteRepositoryWithStorageResult>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.Delete(repositoryId.Value, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static CreateRepositoryRequest CreateRequest(bool isPrivate) =>
        new(DefaultRepositoryName, isPrivate);

    private static RepositoryController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId,
        string username = "testuser",
        IOrganizationAccessService? organizationAccess = null,
        IRepositoryDiskUsageProvider? repositoryDiskUsageProvider = null
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = username }
        );

        return new RepositoryController(
            queryProcessor,
            userContext,
            organizationAccess ?? Substitute.For<IOrganizationAccessService>(),
            new RepositoryStorageQuotaOptions(),
            repositoryDiskUsageProvider ?? Substitute.For<IRepositoryDiskUsageProvider>()
        );
    }

    private static void ConfigureUserLookup(IQueryProcessor queryProcessor, UserId userId)
    {
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(new User { Id = userId, Username = "testuser" }));
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetEmailVerifiedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(true));
    }

    private static UpdateRepositoryRequest CreateUpdateRequest(
        string name,
        bool isPrivate = false
    ) => new(name, isPrivate);

    public class E2ETests : ControllerTestBase
    {
        public E2ETests(WebApplicationFactory<ApiEntryPoint> factory)
            : base(factory) { }

        [Fact]
        public async Task Create_ReturnsCreatedWithId()
        {
            await AuthenticateAsync("repo-create-user", "repo-create@example.com");

            var response = await CreateRepositoryAsync("my-repo", "My Repo", isPrivate: true);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var createdId = await response.Content.ReadFromJsonAsync<RepositoryId>();
            Assert.NotNull(createdId);
            Assert.NotEqual(Guid.Empty, createdId.Value);

            var getResponse = await Client.GetAsync($"/repository/{createdId.Value}");
            getResponse.EnsureSuccessStatusCode();

            var dto = await getResponse.Content.ReadFromJsonAsync<RepositoryDto>();
            Assert.NotNull(dto);
            Assert.Equal("my-repo", dto.Slug);
            Assert.Equal("My Repo", dto.Name);
            Assert.True(dto.IsPrivate);
        }

        [Theory]
        [InlineData("bad_slug")]
        [InlineData("has space")]
        public async Task Create_WhenSlugInvalid_ReturnsBadRequest(string slug)
        {
            await AuthenticateAsync("repo-invalid-slug", "repo-invalid-slug@example.com");

            var response = await CreateRepositoryAsync(slug, "My Repo", isPrivate: false);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_WhenSlugAlreadyTaken_ReturnsBadRequest()
        {
            await AuthenticateAsync("repo-dup-slug", "repo-dup-slug@example.com");

            var first = await CreateRepositoryAsync("duplicate-slug", "First", isPrivate: false);
            first.EnsureSuccessStatusCode();

            var second = await CreateRepositoryAsync("duplicate-slug", "Second", isPrivate: false);

            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        }

        [Fact]
        public async Task Get_WhenMissing_ReturnsNotFound()
        {
            await AuthenticateAsync("repo-get-missing", "repo-get-missing@example.com");

            var response = await Client.GetAsync($"/repository/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task List_WhenEmpty_ReturnsEmptyList()
        {
            await AuthenticateAsync("repo-list-empty", "repo-list-empty@example.com");

            var response = await Client.GetAsync("/repository");
            response.EnsureSuccessStatusCode();

            var repositories = await response.Content.ReadFromJsonAsync<List<RepositoryDto>>();
            Assert.NotNull(repositories);
            Assert.Empty(repositories);
        }

        [Fact]
        public async Task List_ReturnsOnlyCurrentUsersRepositories()
        {
            await AuthenticateAsync("repo-list-owner", "repo-list-owner@example.com");

            var createResponse = await CreateRepositoryAsync("visible-repo", "Visible Repo", false);
            createResponse.EnsureSuccessStatusCode();

            var listResponse = await Client.GetAsync("/repository");
            listResponse.EnsureSuccessStatusCode();

            var repositories = await listResponse.Content.ReadFromJsonAsync<List<RepositoryDto>>();
            Assert.NotNull(repositories);
            Assert.Single(repositories);
            Assert.Equal("visible-repo", repositories[0].Slug);
            Assert.Equal("Visible Repo", repositories[0].Name);

            await AuthenticateAsync("repo-list-other", "repo-list-other@example.com");

            var otherListResponse = await Client.GetAsync("/repository");
            otherListResponse.EnsureSuccessStatusCode();

            var otherRepositories = await otherListResponse.Content.ReadFromJsonAsync<
                List<RepositoryDto>
            >();
            Assert.NotNull(otherRepositories);
            Assert.Empty(otherRepositories);
        }

        [Fact]
        public async Task UpdateMetadata_WhenOwned_ReturnsNoContent()
        {
            await AuthenticateAsync("repo-update-owner", "repo-update-owner@example.com");

            var createResponse = await CreateRepositoryAsync("update-me", "Original", false);
            createResponse.EnsureSuccessStatusCode();
            var createdId = await createResponse.Content.ReadFromJsonAsync<RepositoryId>();
            Assert.NotNull(createdId);

            var updateResponse = await Client.PutAsJsonAsync(
                $"/repository/{createdId.Value}",
                new UpdateRepositoryRequest("Updated name", true)
            );
            Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

            var getResponse = await Client.GetAsync($"/repository/{createdId.Value}");
            getResponse.EnsureSuccessStatusCode();
            var dto = await getResponse.Content.ReadFromJsonAsync<RepositoryDto>();
            Assert.NotNull(dto);
            Assert.Equal("Updated name", dto.Name);
            Assert.True(dto.IsPrivate);
        }

        [Fact]
        public async Task UpdateMetadata_WhenMissing_ReturnsNotFound()
        {
            await AuthenticateAsync("repo-update-missing", "repo-update-missing@example.com");

            var missingId = Guid.NewGuid();
            var response = await Client.PutAsJsonAsync(
                $"/repository/{missingId}",
                new UpdateRepositoryRequest("Updated", false)
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateMetadata_WhenOwnedByOtherUser_ReturnsForbidden()
        {
            await AuthenticateAsync("repo-update-real-owner", "repo-update-real-owner@example.com");

            var createResponse = await CreateRepositoryAsync("protected-repo", "Protected", false);
            createResponse.EnsureSuccessStatusCode();
            var createdId = await createResponse.Content.ReadFromJsonAsync<RepositoryId>();
            Assert.NotNull(createdId);

            await AuthenticateAsync("repo-update-intruder", "repo-update-intruder@example.com");

            var response = await Client.PutAsJsonAsync(
                $"/repository/{createdId.Value}",
                new UpdateRepositoryRequest("Hacked", false)
            );

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Delete_WhenOwned_ReturnsNoContent()
        {
            await AuthenticateAsync("repo-delete-owner", "repo-delete-owner@example.com");

            var createResponse = await CreateRepositoryAsync("delete-me", "Delete me", false);
            createResponse.EnsureSuccessStatusCode();
            var createdId = await createResponse.Content.ReadFromJsonAsync<RepositoryId>();
            Assert.NotNull(createdId);

            var deleteResponse = await Client.DeleteAsync($"/repository/{createdId.Value}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await Client.GetAsync($"/repository/{createdId.Value}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task Delete_WhenMissing_ReturnsNotFound()
        {
            await AuthenticateAsync("repo-delete-missing", "repo-delete-missing@example.com");

            var response = await Client.DeleteAsync($"/repository/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Delete_WhenOwnedByOtherUser_ReturnsForbidden()
        {
            await AuthenticateAsync("repo-delete-real-owner", "repo-delete-real-owner@example.com");

            var createResponse = await CreateRepositoryAsync(
                "protected-delete",
                "Protected",
                false
            );
            createResponse.EnsureSuccessStatusCode();
            var createdId = await createResponse.Content.ReadFromJsonAsync<RepositoryId>();
            Assert.NotNull(createdId);

            await AuthenticateAsync("repo-delete-intruder", "repo-delete-intruder@example.com");

            var response = await Client.DeleteAsync($"/repository/{createdId.Value}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_WhenUnauthenticated_ReturnsUnauthorized()
        {
            Client.DefaultRequestHeaders.Authorization = null;

            var response = await CreateRepositoryAsync("unauth-repo", "Unauth", false);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Create_WhenQueryFails_ReturnsNotFound()
        {
            var userId = UserId.From(Guid.NewGuid());
            var queryProcessor = Substitute.For<IQueryProcessor>();
            ConfigureE2EUserLookup(queryProcessor, userId);
            queryProcessor
                .RunQueryAsync(
                    Arg.Any<GetRepositoryBySlugForUserQuery>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option<RepositoryDto>.None);
            queryProcessor
                .RunQueryAsync(Arg.Any<CreateRepositoryWithStorageQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option<CreateRepositoryWithStorageResult>.None);

            var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-create-user");

            var response = await client.PostAsJsonAsync(
                "/repository/my-repo",
                new CreateRepositoryRequest("Fails", false)
            );

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_WhenQueryReturnsNone_ReturnsNotFound()
        {
            var userId = UserId.From(Guid.NewGuid());
            var queryProcessor = Substitute.For<IQueryProcessor>();
            ConfigureE2EUserLookup(queryProcessor, userId);
            queryProcessor
                .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option<RepositoryDto>.None);

            var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-get-user");
            var response = await client.GetAsync($"/repository/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task List_WhenQueryReturnsNone_ReturnsNotFound()
        {
            var userId = UserId.From(Guid.NewGuid());
            var queryProcessor = Substitute.For<IQueryProcessor>();
            ConfigureE2EUserLookup(queryProcessor, userId);
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<ListRepositoriesForUserQuery>(query => query.UserId == userId),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option<IReadOnlyList<RepositoryDto>>.None);

            var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-list-user");
            var response = await client.GetAsync("/repository");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Delete_WhenDeleteQueryFails_ReturnsNotFound()
        {
            var userId = UserId.From(Guid.NewGuid());
            var repositoryId = RepositoryId.From(Guid.NewGuid());
            var queryProcessor = Substitute.For<IQueryProcessor>();
            ConfigureE2EUserLookup(queryProcessor, userId);
            queryProcessor
                .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
                .Returns(
                    Option.From(
                        new RepositoryDto
                        {
                            Id = repositoryId,
                            OwnerUserId = userId,
                            Slug = "existing",
                            Name = "Existing",
                        }
                    )
                );
            queryProcessor
                .RunQueryAsync(Arg.Any<DeleteRepositoryQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option<Unit>.None);

            var client = CreateAuthenticatedClient(queryProcessor, userId, "mock-delete-user");
            var response = await client.DeleteAsync($"/repository/{repositoryId.Value}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private static void ConfigureE2EUserLookup(IQueryProcessor queryProcessor, UserId userId)
        {
            queryProcessor
                .RunQueryAsync(Arg.Any<UserGetByIdQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option.From(new User { Id = userId, Username = "mockuser" }));
            queryProcessor
                .RunQueryAsync(Arg.Any<UserGetEmailVerifiedQuery>(), Arg.Any<CancellationToken>())
                .Returns(Option.From(true));
        }

        private Task<HttpResponseMessage> CreateRepositoryAsync(
            string slug,
            string repositoryName,
            bool isPrivate
        ) =>
            Client.PostAsJsonAsync(
                $"/repository/{slug}",
                new CreateRepositoryRequest(repositoryName, isPrivate)
            );

        private async Task AuthenticateAsync(string username, string email)
        {
            var token = await RegisterUserAsync(username, email, "Password123!");
            await MarkEmailVerifiedAsync(username);
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token
            );
        }

        private HttpClient CreateAuthenticatedClient(
            IQueryProcessor queryProcessor,
            UserId userId,
            string username
        )
        {
            var (client, _) = CreateClientWithQueryProcessor(queryProcessor);
            var token = JwtTokenGenerator.GetJWTToken(username, userId.Value.ToString());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                token
            );
            return client;
        }
    }
}
