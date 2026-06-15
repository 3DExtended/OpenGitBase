using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Api.Tests.Controllers;

public class FleetBootstrapControllerTests
{
    [Fact]
    public async Task GetDispatcherSshPrivateKey_WhenMissingToken_ReturnsUnauthorized()
    {
        var controller = new FleetBootstrapController(Substitute.For<IQueryProcessor>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.GetDispatcherSshPrivateKey(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }
}
