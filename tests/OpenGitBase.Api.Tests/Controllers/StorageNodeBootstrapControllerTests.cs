using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Controllers;

public class StorageNodeBootstrapControllerTests
{
    [Fact]
    public async Task GetDispatcherSshPublicKey_WhenMissingHeaders_ReturnsUnauthorized()
    {
        var controller = new StorageNodeBootstrapController(Substitute.For<IQueryProcessor>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetDispatcherSshPublicKey(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }
}
