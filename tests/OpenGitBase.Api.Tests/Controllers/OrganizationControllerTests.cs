using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;

namespace OpenGitBase.Api.Tests.Controllers;

public class OrganizationControllerTests
{
    [Fact]
    public async Task List_WhenFound_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListUserOrganizationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From((IReadOnlyList<OrganizationDto>)Array.Empty<OrganizationDto>()));

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.List(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Create_WhenVerified_ReturnsCreated()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetEmailVerifiedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(true));
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(organizationId));

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.Create(
            new CreateOrganizationQuery
            {
                ModelToCreate = new OrganizationDto { Name = "Acme", Slug = "acme" },
            },
            CancellationToken.None
        );

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(organizationId, created.Value);
    }

    [Fact]
    public async Task Get_WhenFound_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
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

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.Get(organizationId.Value, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<OrganizationDto>(ok.Value);
    }

    [Fact]
    public async Task GetBySlug_WhenFound_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationBySlugQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(new OrganizationDto { Name = "Acme", Slug = "acme" })
            );

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.GetBySlug("acme", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ListMembers_WhenFound_ReturnsOk()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
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

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListOrganizationMembersQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    (IReadOnlyList<OrganizationMemberDto>)Array.Empty<OrganizationMemberDto>()
                )
            );

        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.ListMembers(organizationId.Value, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ListMembers_WhenNonMember_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckMemberAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(
                new OrganizationMemberAccessCheck(
                    true,
                    false,
                    false,
                    new OrganizationDto { Id = organizationId, Name = "Acme", Slug = "acme" }
                )
            );

        var queryProcessor = Substitute.For<IQueryProcessor>();
        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.ListMembers(organizationId.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(
                Arg.Any<ListOrganizationMembersQuery>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ListMembers_WhenOrganizationMissing_ReturnsNotFound()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckMemberAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationMemberAccessCheck(false, false, false, null));

        var controller = CreateController(
            Substitute.For<IQueryProcessor>(),
            userId,
            organizationAccess
        );
        var result = await controller.ListMembers(organizationId.Value, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task AddMember_WhenOwner_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
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

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<AddOrganizationMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.AddMember(
            organizationId.Value,
            new OpenGitBase.Api.Models.AddOrganizationMemberRequest
            {
                UserId = Guid.NewGuid(),
                Role = OrganizationMemberRole.Member,
            },
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenNoBlockers_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
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
        organizationAccess
            .GetDeleteBlockersAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OrganizationDeleteBlocker>());

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<DeleteOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.Delete(organizationId.Value, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task RemoveMember_WhenNotLastOwner_ReturnsNoContent()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var memberUserId = Guid.NewGuid();
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
        organizationAccess
            .WouldRemoveLastOwnerAsync(
                organizationId,
                UserId.From(memberUserId),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<RemoveOrganizationMemberQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.RemoveMember(
            organizationId.Value,
            memberUserId,
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Create_WhenEmailNotVerified_ReturnsForbidden()
    {
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UserGetEmailVerifiedQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From(false));

        var controller = CreateController(queryProcessor, userId);
        var result = await controller.Create(
            new CreateOrganizationQuery
            {
                ModelToCreate = new OrganizationDto
                {
                    Name = "Acme",
                    Slug = "acme",
                },
            },
            CancellationToken.None
        );

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Update_WhenNonOwner_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckOwnerAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(
                new OrganizationOwnerAccessCheck(
                    true,
                    false,
                    new OrganizationDto
                    {
                        Id = organizationId,
                        Name = "Acme",
                        Slug = "acme",
                        OwnerUserId = Guid.NewGuid(),
                    }
                )
            );

        var controller = CreateController(
            Substitute.For<IQueryProcessor>(),
            userId,
            organizationAccess
        );
        var result = await controller.Update(
            organizationId.Value,
            new UpdateOrganizationQuery
            {
                UpdatedModel = new OrganizationDto { Name = "Acme Labs" },
            },
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Update_WhenOwner_UpdatesNameOnly()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var ownerUserId = Guid.NewGuid();
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckOwnerAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(
                new OrganizationOwnerAccessCheck(
                    true,
                    true,
                    new OrganizationDto
                    {
                        Id = organizationId,
                        Name = "Acme",
                        Slug = "acme",
                        OwnerUserId = ownerUserId,
                    }
                )
            );

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<UpdateOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(Unit.Value);

        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.Update(
            organizationId.Value,
            new UpdateOrganizationQuery
            {
                UpdatedModel = new OrganizationDto
                {
                    Name = "Acme Labs",
                    Slug = "changed-slug",
                    OwnerUserId = Guid.NewGuid(),
                },
            },
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
        await queryProcessor
            .Received(1)
            .RunQueryAsync(
                Arg.Is<UpdateOrganizationQuery>(query =>
                    query.UpdatedModel.Name == "Acme Labs"
                    && query.UpdatedModel.Slug == "acme"
                    && query.UpdatedModel.OwnerUserId == ownerUserId
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Delete_WhenRepositoriesExist_ReturnsConflictWithBlockers()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
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
        organizationAccess
            .GetDeleteBlockersAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(
                new List<OrganizationDeleteBlocker>
                {
                    new()
                    {
                        Type = "repository",
                        Name = "App",
                        Slug = "app",
                    },
                }
            );

        var queryProcessor = Substitute.For<IQueryProcessor>();
        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.Delete(organizationId.Value, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var payload = Assert.IsType<OrganizationDeleteBlockedResult>(conflict.Value);
        Assert.False(payload.Success);
        Assert.Single(payload.Blockers);
        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(Arg.Any<DeleteOrganizationQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveMember_WhenLastOwner_ReturnsConflict()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var memberUserId = Guid.NewGuid();
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
        organizationAccess
            .WouldRemoveLastOwnerAsync(
                organizationId,
                UserId.From(memberUserId),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var queryProcessor = Substitute.For<IQueryProcessor>();
        var controller = CreateController(queryProcessor, userId, organizationAccess);
        var result = await controller.RemoveMember(
            organizationId.Value,
            memberUserId,
            CancellationToken.None
        );

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(conflict.Value);
        await queryProcessor
            .DidNotReceive()
            .RunQueryAsync(
                Arg.Any<RemoveOrganizationMemberQuery>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task AddMember_WhenNonOwner_ReturnsForbid()
    {
        var userId = UserId.From(Guid.NewGuid());
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var organizationAccess = Substitute.For<IOrganizationAccessService>();
        organizationAccess
            .CheckOwnerAccessAsync(organizationId, userId, Arg.Any<CancellationToken>())
            .Returns(new OrganizationOwnerAccessCheck(true, false, new OrganizationDto()));

        var controller = CreateController(
            Substitute.For<IQueryProcessor>(),
            userId,
            organizationAccess
        );
        var result = await controller.AddMember(
            organizationId.Value,
            new OpenGitBase.Api.Models.AddOrganizationMemberRequest
            {
                UserId = Guid.NewGuid(),
                Role = OrganizationMemberRole.Member,
            },
            CancellationToken.None
        );

        Assert.IsType<ForbidResult>(result);
    }

    private static OrganizationController CreateController(
        IQueryProcessor queryProcessor,
        UserId userId,
        IOrganizationAccessService? organizationAccess = null
    )
    {
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity
            {
                IdentityProviderId = userId.Value.ToString(),
                Username = "orguser",
            }
        );

        return new OrganizationController(
            queryProcessor,
            userContext,
            organizationAccess ?? Substitute.For<IOrganizationAccessService>()
        );
    }
}
