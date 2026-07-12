using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class OrganizationStorageControllerTests
{
    [Fact]
    public async Task UpdateCapacity_WhenNotOwner_ReturnsForbid()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = "member" }
        );

        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckOwnerAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationOwnerAccessCheck(true, false, null));

        var controller = CreateController(queryProcessor, userContext, organizationAccess);

        var result = await controller.UpdateCapacity(
            organizationId.Value,
            Guid.NewGuid(),
            new UpdateStorageNodeCapacityRequest { MaxBytes = 1_000_000 },
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCapacity_WhenNodeNotInOrg_ReturnsNotFound()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var storageNodeId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<StorageNodeDto>>(
                    Array.Empty<StorageNodeDto>()
                )
            );

        var controller = CreateController(
            queryProcessor,
            CreateOwnerContext(userId),
            CreateOwnerAccess(organizationId, userId)
        );

        var result = await controller.UpdateCapacity(
            organizationId.Value,
            storageNodeId,
            new UpdateStorageNodeCapacityRequest { MaxBytes = 1_000_000 },
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCapacity_WhenBelowUsedBytes_ReturnsBadRequest()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var storageNodeId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<StorageNodeDto>>(
                    new[]
                    {
                        new StorageNodeDto
                        {
                            Id = StorageNodeId.From(storageNodeId),
                            NodeId = "org-storage-1",
                            InternalHost = "host",
                            InternalHttpPort = 8081,
                            UsedBytes = 500_000,
                            MaxBytes = 1_000_000,
                        },
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateStorageNodeCapacityQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<StorageNodeDto>.None);

        var controller = CreateController(
            queryProcessor,
            CreateOwnerContext(userId),
            CreateOwnerAccess(organizationId, userId)
        );

        var result = await controller.UpdateCapacity(
            organizationId.Value,
            storageNodeId,
            new UpdateStorageNodeCapacityRequest { MaxBytes = 400_000 },
            CancellationToken.None
        );

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UpdateCapacity_WhenOwnerAndValid_ReturnsUpdatedNode()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var storageNodeId = Guid.NewGuid();
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationStorageNodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From<IReadOnlyList<StorageNodeDto>>(
                    new[]
                    {
                        new StorageNodeDto
                        {
                            Id = StorageNodeId.From(storageNodeId),
                            NodeId = "org-storage-1",
                            InternalHost = "host",
                            InternalHttpPort = 8081,
                            UsedBytes = 500_000,
                            MaxBytes = 1_000_000,
                        },
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateStorageNodeCapacityQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new StorageNodeDto
                    {
                        Id = StorageNodeId.From(storageNodeId),
                        NodeId = "org-storage-1",
                        InternalHost = "host",
                        InternalHttpPort = 8081,
                        UsedBytes = 500_000,
                        MaxBytes = 2_000_000,
                    }
                )
            );

        var controller = CreateController(
            queryProcessor,
            CreateOwnerContext(userId),
            CreateOwnerAccess(organizationId, userId)
        );

        var result = await controller.UpdateCapacity(
            organizationId.Value,
            storageNodeId,
            new UpdateStorageNodeCapacityRequest { MaxBytes = 2_000_000 },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<StorageNodeDto>(ok.Value);
        Assert.Equal(2_000_000, dto.MaxBytes);
    }

    private static OrganizationStorageController CreateController(
        IQueryProcessor queryProcessor,
        IUserContext userContext,
        IOrganizationAccessService organizationAccess
    ) =>
        new(queryProcessor, userContext, organizationAccess, new RepositoryStorageQuotaOptions());

    private static IUserContext CreateOwnerContext(UserId userId)
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = "owner" }
        );
        return userContext;
    }

    private static IOrganizationAccessService CreateOwnerAccess(
        OrganizationId organizationId,
        UserId userId
    )
    {
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckOwnerAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(
                new OrganizationOwnerAccessCheck(
                    true,
                    true,
                    new OrganizationDto { Id = organizationId, Name = "Acme", Slug = "acme" }
                )
            );
        return organizationAccess;
    }
}
