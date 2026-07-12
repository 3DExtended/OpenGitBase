using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Controllers;

public class OrganizationComputeControllerTests
{
    [Fact]
    public async Task ListNodes_WhenNotOwner_ReturnsForbid()
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

        var controller = new OrganizationComputeController(
            queryProcessor,
            userContext,
            organizationAccess
        );

        var result = await controller.ListNodes(organizationId.Value, CancellationToken.None);

        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task CreateEnrollment_WhenOwner_ReturnsEnrollment()
    {
        var organizationId = OrganizationId.From(Guid.NewGuid());
        var userId = UserId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateComputeNodeEnrollmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new CreateComputeNodeEnrollmentResult
                    {
                        EnrollmentId = Guid.NewGuid(),
                        NodeId = "org-compute-1",
                        EnrollmentToken = "token",
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(6),
                    }
                )
            );

        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = userId.Value.ToString(), Username = "owner" }
        );

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

        var controller = new OrganizationComputeController(
            queryProcessor,
            userContext,
            organizationAccess
        );

        var result = await controller.CreateEnrollment(
            organizationId.Value,
            new OpenGitBase.Api.Models.CreateComputeNodeEnrollmentRequest
            {
                NodeId = "org-compute-1",
                MaxConcurrentJobs = 1,
                MaxCpu = 1,
                MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<CreateComputeNodeEnrollmentResult>(ok.Value);
    }
}
