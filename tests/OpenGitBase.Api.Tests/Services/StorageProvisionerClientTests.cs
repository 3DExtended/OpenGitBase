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
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(
            async (request, _) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal(
                    "http://storage-1:8081/internal/repos",
                    request.RequestUri?.ToString()
                );
                Assert.Equal("Bearer secret-token", request.Headers.Authorization?.ToString());
                requestBody = request.Content is null
                    ? null
                    : await request.Content.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
        );

        var client = new StorageProvisionerClient(new HttpClient(handler));
        var node = CreateNode();

        var result = await client.ProvisionRepositoryAsync(
            node,
            "secret-token",
            "/srv/git/repo.git",
            52_428_800,
            cancellationToken: CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Contains("\"physicalPath\":\"/srv/git/repo.git\"", requestBody);
        Assert.Contains("\"receiveMaxBytes\":52428800", requestBody);
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
            cancellationToken: CancellationToken.None
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
            cancellationToken: CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task UploadReplicationArtifactAsync_SendsTheManifestAndAHexBundleWithContentLength()
    {
        byte[]? requestBody = null;
        long? contentLength = null;
        var handler = new StubHttpMessageHandler(
            async (request, _) =>
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal(
                    "http://storage-1:8081/internal/repos/0b9c7c41-5e1b-4a8f-9d3e-0c6f1f6d2a11/artifacts/7",
                    request.RequestUri?.ToString()
                );
                Assert.Equal("application/json", request.Content?.Headers.ContentType?.MediaType);
                contentLength = request.Content?.Headers.ContentLength;
                requestBody = await request.Content!.ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
        );
        var client = new StorageProvisionerClient(new HttpClient(handler));

        var result = await client.UploadReplicationArtifactAsync(
            CreateNode(),
            "secret-token",
            Guid.Parse("0b9c7c41-5e1b-4a8f-9d3e-0c6f1f6d2a11"),
            7,
            """{ "watermark": 7, "keyId": "k1" }""",
            [0x00, 0xAB, 0xFF, 0x10],
            CancellationToken.None
        );

        Assert.True(result.Success);
        Assert.Equal(requestBody!.Length, contentLength);
        using var json = System.Text.Json.JsonDocument.Parse(requestBody);
        Assert.Equal(7, json.RootElement.GetProperty("manifest").GetProperty("watermark").GetInt32());
        Assert.Equal("k1", json.RootElement.GetProperty("manifest").GetProperty("keyId").GetString());
        Assert.Equal("00abff10", json.RootElement.GetProperty("bundleBase64").GetString());
    }

    [Fact]
    public async Task UploadReplicationArtifactAsync_UploadsABundleBeyondTheJsonWriterValueLimit()
    {
        // System.Text.Json refuses string values above 166,666,666 characters; this bundle's hex is 168,000,000.
        var bundle = new byte[84_000_000];
        new Random(413).NextBytes(bundle);
        byte[]? requestBody = null;
        var handler = new StubHttpMessageHandler(
            async (request, _) =>
            {
                requestBody = await request.Content!.ReadAsByteArrayAsync();
                return new HttpResponseMessage(HttpStatusCode.Created);
            }
        );
        var client = new StorageProvisionerClient(new HttpClient(handler));

        var result = await client.UploadReplicationArtifactAsync(
            CreateNode(),
            "secret-token",
            Guid.NewGuid(),
            1,
            """{"watermark":1}""",
            bundle,
            CancellationToken.None
        );

        Assert.True(result.Success);
        var prefix = Encoding.UTF8.GetBytes("{\"manifest\":{\"watermark\":1},\"bundleBase64\":\"");
        var suffix = Encoding.UTF8.GetBytes("\"}");
        Assert.Equal(prefix.Length + (2 * bundle.Length) + suffix.Length, requestBody!.Length);
        Assert.True(requestBody.AsSpan(0, prefix.Length).SequenceEqual(prefix));
        Assert.True(requestBody.AsSpan(requestBody.Length - suffix.Length).SequenceEqual(suffix));
        for (var offset = 0; offset < bundle.Length; offset += 1_000_000)
        {
            var chunk = bundle.AsSpan(offset, Math.Min(1_000_000, bundle.Length - offset));
            var expectedHex = Encoding.ASCII.GetBytes(Convert.ToHexStringLower(chunk));
            Assert.True(requestBody.AsSpan(prefix.Length + (2 * offset), expectedHex.Length).SequenceEqual(expectedHex));
        }
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
