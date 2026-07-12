using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Api.Tests.Controllers;

public class ComputeNodeControllerTests
{
    [Fact]
    public async Task Register_WhenSuccessful_ReturnsNodeAndToken()
    {
        var nodeId = ComputeNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<RegisterComputeNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RegisterComputeNodeResultDto
                    {
                        Node = new ComputeNodeDto
                        {
                            Id = nodeId,
                            NodeId = "compute-1",
                            IsHealthy = true,
                            LastHeartbeatAt = DateTimeOffset.UtcNow,
                        },
                        NodeIdentityToken = $"{nodeId.Value:D}:secret",
                    }
                )
            );

        var userContext = Substitute.For<IUserContext>();
        var identityService = new ComputeNodeIdentityService(
            Substitute.For<Microsoft.EntityFrameworkCore.IDbContextFactory<OpenGitBase.Common.Data.OpenGitBaseDbContext>>(),
            Substitute.For<OpenGitBase.Common.Services.IPasswordHasherService>()
        );
        var controller = new ComputeNodeController(queryProcessor, userContext, identityService);
        var result = await controller.Register(
            new RegisterComputeNodeQuery
            {
                NodeId = "compute-1",
                EnrollmentToken = "token",
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RegisterComputeNodeResultDto>(ok.Value);
        Assert.Equal(nodeId, dto.Node.Id);
        Assert.NotEmpty(dto.NodeIdentityToken);
    }

    [Fact]
    public async Task Heartbeat_WithoutBearer_ReturnsUnauthorized()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var userContext = Substitute.For<IUserContext>();
        var identityService = new ComputeNodeIdentityService(
            Substitute.For<Microsoft.EntityFrameworkCore.IDbContextFactory<OpenGitBase.Common.Data.OpenGitBaseDbContext>>(),
            Substitute.For<OpenGitBase.Common.Services.IPasswordHasherService>()
        );
        var controller = new ComputeNodeController(queryProcessor, userContext, identityService);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        var result = await controller.Heartbeat(
            new ComputeNodeHeartbeatQuery { NodeId = "compute-1", RunningJobs = 0 },
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedResult>(result);
    }
}
