using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminComputeEnrollmentsControllerTests
{
    [Fact]
    public async Task List_ReturnsEnrollments()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<ListComputeNodeEnrollmentsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option.From<IReadOnlyList<ComputeNodeEnrollmentDto>>(Array.Empty<ComputeNodeEnrollmentDto>()));

        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity { IdentityProviderId = Guid.NewGuid().ToString(), Username = "admin" }
        );

        var controller = new AdminComputeEnrollmentsController(queryProcessor, userContext);

        var result = await controller.List(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task Create_WhenSuccessful_ReturnsEnrollment()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<CreateComputeNodeEnrollmentQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new CreateComputeNodeEnrollmentResult
                    {
                        EnrollmentId = Guid.NewGuid(),
                        NodeId = "compute-agent-1",
                        EnrollmentToken = "secret-token",
                        ExpiresAt = DateTimeOffset.UtcNow.AddHours(6),
                    }
                )
            );

        var userId = Guid.NewGuid();
        var userContext = Substitute.For<IUserContext>();
        userContext.User.Returns(
            new UserIdentity
            {
                IdentityProviderId = userId.ToString(),
                Username = "admin",
            }
        );

        var controller = new AdminComputeEnrollmentsController(queryProcessor, userContext);

        var result = await controller.Create(
            new OpenGitBase.Api.Models.CreateComputeNodeEnrollmentRequest
            {
                NodeId = "compute-agent-1",
                MaxConcurrentJobs = 2,
                MaxCpu = 2,
                MaxMemoryBytes = 2L * 1024 * 1024 * 1024,
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<CreateComputeNodeEnrollmentResult>(ok.Value);
        Assert.Equal("compute-agent-1", dto.NodeId);
    }
}
