using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class FleetComponentRegistryControllerTests
{
    [Fact]
    public async Task Register_ReturnsOkWithHeartbeatInterval()
    {
        var processor = Substitute.For<IQueryProcessor>();
        processor
            .RunQueryAsync(Arg.Any<RegisterFleetComponentQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RegisterFleetComponentResult
                    {
                        FleetComponentId = FleetComponentId.From(Guid.NewGuid()),
                        HeartbeatIntervalSeconds = 30,
                    }
                )
            );

        var controller = new FleetComponentRegistryController(processor);
        var response = await controller.Register(
            new RegisterFleetComponentRequest
            {
                ComponentType = "Api",
                InstanceId = "api-1",
                ProbeUrl = "http://api-1:8080/health",
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<RegisterFleetComponentResponse>(ok.Value);
        Assert.Equal(30, payload.HeartbeatIntervalSeconds);
    }

    [Fact]
    public async Task Heartbeat_WhenUnknownComponent_ReturnsNotFound()
    {
        var processor = Substitute.For<IQueryProcessor>();
        processor
            .RunQueryAsync(Arg.Any<FleetComponentHeartbeatQuery>(), Arg.Any<CancellationToken>())
            .Returns(Option<FleetComponentHeartbeatResult>.None);

        var controller = new FleetComponentRegistryController(processor);
        var response = await controller.Heartbeat(
            new FleetComponentHeartbeatRequest
            {
                ComponentType = "Api",
                InstanceId = "missing",
            },
            CancellationToken.None
        );

        Assert.IsType<NotFoundResult>(response.Result);
    }
}
