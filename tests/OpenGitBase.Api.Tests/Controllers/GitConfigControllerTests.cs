using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;

namespace OpenGitBase.Api.Tests.Controllers;

public class GitConfigControllerTests : ControllerTestBase
{
    public GitConfigControllerTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task GetConfig_ReturnsGitBaseUrlAndSshEnabled()
    {
        var response = await Client.GetAsync("/api/v1/git/config");
        response.EnsureSuccessStatusCode();

        var config = await response.Content.ReadFromJsonAsync<GitConfigResponse>();
        Assert.NotNull(config);
        Assert.False(string.IsNullOrWhiteSpace(config.GitBaseUrl));
        Assert.False(config.SshEnabled);
    }
}
