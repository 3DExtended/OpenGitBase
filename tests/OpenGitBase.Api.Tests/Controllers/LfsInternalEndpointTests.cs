using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Tests.Base;

namespace OpenGitBase.Api.Tests.Controllers;

/// <summary>The LFS endpoints through the real pipeline: routing, DI and the LFS wire format.</summary>
public class LfsInternalEndpointTests : ControllerTestBase
{
    public LfsInternalEndpointTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task Batch_WhenLfsIsDisabled_Answers501InTheLfsErrorFormat()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/v1/internal/lfs/batch",
            new
            {
                accessToken = "ogb_pat",
                repositoryPath = "alice/repo",
                operation = "download",
                objects = new[] { new { oid = new string('a', 64), size = 1 } },
            }
        );

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Git LFS is not enabled on this server.", body.RootElement.GetProperty("message").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("request_id").GetString()));
    }
}
