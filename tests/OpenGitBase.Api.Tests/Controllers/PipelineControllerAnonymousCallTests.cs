using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Tests.Base;

namespace OpenGitBase.Api.Tests.Controllers;

/// <summary>
/// The endpoints of <see cref="OpenGitBase.Api.Controllers.PipelineController"/> that storage nodes
/// and compute agents call carry no user JWT. Hosted, so the controller is built by DI the way it
/// is in production: a constructor that asks for the signed-in user fails every such call with 500
/// before the action runs, which unit tests with a substituted user context cannot see.
/// </summary>
public class PipelineControllerAnonymousCallTests : ControllerTestBase
{
    public PipelineControllerAnonymousCallTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory) { }

    [Fact]
    public async Task IngestGitPush_FromStorageNodeWithoutJwt_IsAccepted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/pipelines/git-push-ingest")
        {
            Content = JsonContent.Create(new
            {
                repositoryId = Guid.NewGuid(),
                @ref = "refs/heads/main",
                afterSha = "0123456789abcdef0123456789abcdef01234567",
            }),
        };
        request.Headers.Add("X-Storage-Node-Id", "storage-1");

        using var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task ListRepositoryRuns_Anonymous_DoesNotFailOnMissingUser()
    {
        using var response = await Client.GetAsync($"/repository/{Guid.NewGuid()}/pipelines");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
