using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using OpenGitBase.Api.Controllers.Internal;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.SendGrid;

namespace OpenGitBase.Api.Tests.Controllers;

public class E2eControllerTests
{
    [Fact]
    public void GetEmails_WhenE2eDisabled_ReturnsNotFound()
    {
        var controller = CreateController(environmentName: "Production", captureEmail: false);

        var result = controller.GetEmails(to: null);

        Assert.IsType<NotFoundResult>(result);
    }

    private static E2eController CreateController(string environmentName, bool captureEmail)
    {
        var environment = Substitute.For<IWebHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        if (captureEmail)
        {
            configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["E2E:CaptureEmail"] = "true",
                })
                .Build();
        }

        return new E2eController(
            new CapturingEmailStore(),
            Substitute.For<IDbContextFactory<OpenGitBaseDbContext>>(),
            environment,
            configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }
}
