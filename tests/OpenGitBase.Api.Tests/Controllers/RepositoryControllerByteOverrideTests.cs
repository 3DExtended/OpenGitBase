using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class RepositoryControllerByteOverrideTests
{
    [Fact]
    public async Task GetByteOverrideEligibility_WhenMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.GetByteOverrideEligibility(
            Guid.NewGuid(),
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetByteOverrideEligibility_WhenNotManager_ReturnsForbid()
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
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.GetByteOverrideEligibility(
            repositoryId.Value,
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task GetByteOverrideEligibility_WhenOwner_ReturnsOk()
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
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetRepositoryByteOverrideEligibilityQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryByteOverrideEligibilityDto
                    {
                        Eligible = true,
                        Reason = "Eligible for per-repository byte override.",
                        OrgContributedNodeCount = 4,
                        MaxAllowedOverride = 4_000_000_000,
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.GetByteOverrideEligibility(
            repositoryId.Value,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RepositoryByteOverrideEligibilityDto>(ok.Value);
        Assert.True(dto.Eligible);
    }

    [Fact]
    public async Task GetByteOverrideEligibility_WhenOrgOwner_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var repositoryId = RepositoryId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetRepositoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = UserId.From(organizationId.Value),
                        Slug = "repo",
                        Name = "Repo",
                        OwnerKind = "organization",
                    }
                )
            );
        organizationAccess
            .CheckOwnerAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(
                new OrganizationOwnerAccessCheck(
                    true,
                    true,
                    new OrganizationDto { Id = organizationId, Name = "Acme", Slug = "acme" }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Any<GetRepositoryByteOverrideEligibilityQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryByteOverrideEligibilityDto
                    {
                        Eligible = false,
                        Reason = "Organization must operate more than three healthy storage nodes.",
                    }
                )
            );

        var controller = CreateController(
            queryProcessor,
            userId,
            organizationAccess: organizationAccess
        );

        var result = await controller.GetByteOverrideEligibility(
            repositoryId.Value,
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RepositoryByteOverrideEligibilityDto>(ok.Value);
        Assert.False(dto.Eligible);
    }

    [Fact]
    public async Task UpdateMaxBytesOverride_WhenOwnerAndEligible_ReturnsOk()
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
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Any<UpdateRepositoryMaxBytesOverrideQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryDto
                    {
                        Id = repositoryId,
                        OwnerUserId = userId,
                        Slug = "repo",
                        Name = "Repo",
                        MaxBytesOverride = 5_368_709_120,
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMaxBytesOverride(
            repositoryId.Value,
            new UpdateRepositoryMaxBytesOverrideRequest { MaxBytesOverride = 5_368_709_120 },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RepositoryDto>(ok.Value);
        Assert.Equal(5_368_709_120, dto.MaxBytesOverride);
    }

    [Fact]
    public async Task UpdateMaxBytesOverride_WhenNotEligible_ReturnsNotFound()
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
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Any<UpdateRepositoryMaxBytesOverrideQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option<RepositoryDto>.None);

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMaxBytesOverride(
            repositoryId.Value,
            new UpdateRepositoryMaxBytesOverrideRequest { MaxBytesOverride = 1_000 },
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateMaxBytesOverride_WhenNotManager_ReturnsForbid()
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
                        Slug = "repo",
                        Name = "Repo",
                    }
                )
            );

        var controller = CreateController(queryProcessor, userId);

        var result = await controller.UpdateMaxBytesOverride(
            repositoryId.Value,
            new UpdateRepositoryMaxBytesOverrideRequest { MaxBytesOverride = 1_000 },
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    private static RepositoryController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId,
        IOrganizationAccessService? organizationAccess = null
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = "testuser" }
        );

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext());

        return new RepositoryController(
            queryProcessor,
            userContext,
            organizationAccess ?? Substitute.For<IOrganizationAccessService>(),
            new RepositoryStorageQuotaOptions(),
            Substitute.For<IRepositoryDiskUsageProvider>(),
            new RepositoryContentAuthorizationService(queryProcessor, accessor),
            new RepositoryResponseMapper(accessor)
        );
    }
}
