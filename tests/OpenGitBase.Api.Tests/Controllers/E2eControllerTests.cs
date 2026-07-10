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
    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", false)]
    [InlineData("E2ETest", false)]
    public void GetEmails_WhenE2eDisabled_ReturnsNotFound(string environmentName, bool captureEmail)
    {
        var controller = CreateController(environmentName, captureEmail);

        var result = controller.GetEmails(to: null);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("E2ETest")]
    public void ResetDatabase_WhenCaptureEmailEnabled_AllowsConfiguredEnvironments(string environmentName)
    {
        var controller = CreateController(environmentName, captureEmail: true);

        var result = controller.GetEmails(to: null);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ResetDatabase_WhenProductionEvenWithCaptureEmail_ReturnsNotFound()
    {
        var controller = CreateController(environmentName: "Production", captureEmail: true);

        var result = await controller.ResetDatabaseAsync(CancellationToken.None);

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
