using System.Net;
using System.Text;
using OpenGitBase.Api.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class StorageProvisionerClientTests
{
    [Fact]
    public async Task ProvisionRepositoryAsync_WhenSuccessful_ReturnsOk()
    {
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    "http://storage-1:8081/internal/repos",
                    request.RequestUri?.ToString()
                );
                Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
            }
        );

        var client = new StorageProvisionerClient(new HttpClient(handler));
        var node = CreateNode();

        var result = await client.ProvisionRepositoryAsync(
            node,
            "secret-token",
            "/srv/git/repo.git",
            52_428_800,
            CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
    }

    [Fact]
    public async Task ProvisionRepositoryAsync_WhenUnauthorized_ReturnsFailure()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized))
        );

        var client = new StorageProvisionerClient(new HttpClient(handler));

        var result = await client.ProvisionRepositoryAsync(
            CreateNode(),
            "wrong-token",
            "/srv/git/repo.git",
            52_428_800,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task DeleteRepositoryAsync_WhenSuccessful_ReturnsOk()
    {
        var handler = new StubHttpMessageHandler(
            (request, _) =>
            {
                Assert.Equal(HttpMethod.Delete, request.Method);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        );

        var client = new StorageProvisionerClient(new HttpClient(handler));

        var result = await client.DeleteRepositoryAsync(
            CreateNode(),
            "secret-token",
            "/srv/git/repo.git",
            CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task ProvisionRepositoryAsync_WhenTokenMissing_ReturnsFailure()
    {
        var client = new StorageProvisionerClient(new HttpClient(new StubHttpMessageHandler()));

        var result = await client.ProvisionRepositoryAsync(
            CreateNode(),
            string.Empty,
            "/srv/git/repo.git",
            52_428_800,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    private static StorageNodeDto CreateNode() =>
        new()
        {
            Id = StorageNodeId.From(Guid.NewGuid()),
            NodeId = "storage-1",
            InternalHost = "storage-1",
            InternalHttpPort = 8081,
            IsHealthy = true,
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? handler = null
        )
        {
            _handler =
                handler
                ?? ((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => _handler(request, cancellationToken);
    }
}
