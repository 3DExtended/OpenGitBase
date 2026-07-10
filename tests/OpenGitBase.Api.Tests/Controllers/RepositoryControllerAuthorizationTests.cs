using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryControllerAuthorizationTests
{
    [Fact]
    public async Task GetByOwnerSlug_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var controller = CreateController(queryProcessor, authenticatedUserId: null);

        var result = await controller.GetByOwnerSlug("owner", "repo", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByOwnerSlug_PrivateRepositoryOutsider_ReturnsForbid()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var outsiderId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, authenticatedUserId: outsiderId);

        var result = await controller.GetByOwnerSlug("owner", "repo", CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetByOwnerSlug_PrivateRepositoryMember_ReturnsRedactedSummary()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var memberId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
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

        var controller = CreateController(queryProcessor, authenticatedUserId: memberId);

        var result = await controller.GetByOwnerSlug("owner", "repo", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<RepositorySummaryResponse>(ok.Value);
        Assert.Equal(repository.Id, summary.Id);
    }

    [Fact]
    public async Task Get_PrivateRepositoryAnonymous_ReturnsNotFound()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var controller = CreateController(queryProcessor, authenticatedUserId: null);

        var result = await controller.Get(repository.Id.Value, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetUsage_PrivateRepositoryOutsider_ReturnsForbid()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var outsiderId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: true);
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryMemberDto>.None);

        var controller = CreateController(queryProcessor, authenticatedUserId: outsiderId);

        var result = await controller.GetUsage(repository.Id.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetByOwnerSlug_OmitsInfrastructureFieldsForExternalCallers()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        repository.PhysicalPath = "/srv/git/secret.git";
        repository.StorageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var controller = CreateController(queryProcessor, authenticatedUserId: null);

        var result = await controller.GetByOwnerSlug("owner", "repo", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<RepositorySummaryResponse>(ok.Value);
    }

    [Fact]
    public async Task GetByOwnerSlug_InternalCaller_ReturnsFullRepositoryDto()
    {
        var ownerId = UserId.From(Guid.NewGuid());
        var repository = CreateRepository(ownerId, isPrivate: false);
        repository.PhysicalPath = "/srv/git/secret.git";
        repository.StorageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryByOwnerSlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(repository));

        var controller = CreateController(
            queryProcessor,
            authenticatedUserId: null,
            remoteIpAddress: IPAddress.Loopback
        );

        var result = await controller.GetByOwnerSlug("owner", "repo", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<RepositoryDto>(ok.Value);
        Assert.Equal("/srv/git/secret.git", returned.PhysicalPath);
        Assert.Equal(repository.StorageNodeId, returned.StorageNodeId);
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

    private static RepositoryController CreateController(
        IQueryProcessor queryProcessor,
        UserId? authenticatedUserId,
        IPAddress? remoteIpAddress = null
    )
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIpAddress ?? IPAddress.Parse("203.0.113.10");
        if (authenticatedUserId is not null)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim("identityproviderid", authenticatedUserId.Value.ToString())],
                    authenticationType: "Test"
                )
            );
        }

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        return new RepositoryController(
            queryProcessor,
            Substitute.For<IUserContext>(),
            Substitute.For<IOrganizationAccessService>(),
            new RepositoryStorageQuotaOptions(),
            Substitute.For<IRepositoryDiskUsageProvider>(),
            new RepositoryContentAuthorizationService(queryProcessor, accessor),
            new RepositoryResponseMapper(accessor)
        );
    }
}
