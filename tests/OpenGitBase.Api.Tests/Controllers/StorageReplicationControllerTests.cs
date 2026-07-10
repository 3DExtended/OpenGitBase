using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Api.Tests.Controllers;

public class StorageReplicationControllerTests
{
    [Fact]
    public async Task QuorumReplicate_WhenUnauthorized_ReturnsUnauthorized()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
        var controller = new StorageReplicationController(queryProcessor, repositoryKeyService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.QuorumReplicate(
            Guid.NewGuid(),
            new QuorumReplicateRepositoryRequest { AppliedWatermark = 1 },
            CancellationToken.None
        );

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetReplicationContext_WhenUnauthorized_ReturnsUnauthorized()
    {
        var queryProcessor = Substitute.For<IQueryProcessor>();
        var repositoryKeyService = Substitute.For<IRepositoryKeyService>();
        var controller = new StorageReplicationController(queryProcessor, repositoryKeyService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetReplicationContext(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }
}
