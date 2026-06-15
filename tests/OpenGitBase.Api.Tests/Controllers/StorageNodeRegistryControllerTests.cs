using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class StorageNodeRegistryControllerTests
{
    [Fact]
    public async Task Register_WhenSuccessful_ReturnsToken()
    {
        var storageNodeId = StorageNodeId.From(Guid.NewGuid());
        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<RegisterStorageNodeQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new RegisterStorageNodeResult
                    {
                        StorageNodeId = storageNodeId,
                        ApiToken = "token",
                        HeartbeatIntervalSeconds = 30,
                    }
                )
            );

        var controller = new StorageNodeRegistryController(queryProcessor)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Register(
            new RegisterStorageNodeRequest
            {
                NodeId = "storage-1",
                InternalHost = "storage-1",
                InternalHttpPort = 8081,
            },
            CancellationToken.None
        );

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<RegisterStorageNodeResponse>(ok.Value);
        Assert.Equal(storageNodeId.Value, response.StorageNodeId);
        Assert.Equal("token", response.ApiToken);
    }
}
