using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Common.Models.HealthCheck;

namespace OpenGitBase.Api.Tests.Controllers;

public class HealthControllerTests : IClassFixture<WebApplicationFactory<ApiEntryPoint>>
{
    private readonly WebApplicationFactory<ApiEntryPoint> _factory;

    public HealthControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_returns_health_check_report()
    {
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("E2ETest"))
            .CreateClient();

        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        var report = await response.Content.ReadFromJsonAsync<HealthCheckReport>();
        Assert.NotNull(report);
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.NotEmpty(report.Results);
    }
}
