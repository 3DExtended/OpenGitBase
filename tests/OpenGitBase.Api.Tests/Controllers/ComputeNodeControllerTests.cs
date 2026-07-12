using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class ComputeNodeControllerTests
{
    [Fact]
    public async Task Register_WhenSuccessful_ReturnsNode()
    {
        var nodeId = ComputeNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<RegisterComputeNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new ComputeNodeDto
                    {
                        Id = nodeId,
                        NodeId = "compute-1",
                        IsHealthy = true,
                        LastHeartbeatAt = DateTimeOffset.UtcNow,
                    }
                )
            );

        var userContext = Substitute.For<IUserContext>();
        var controller = new ComputeNodeController(queryProcessor, userContext);
        var result = await controller.Register(
            new RegisterComputeNodeQuery
            {
                NodeId = "compute-1",
                EnrollmentToken = "token",
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ComputeNodeDto>(ok.Value);
        Assert.Equal(nodeId, dto.Id);
    }
}
