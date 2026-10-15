using System.Net;
using OpenGitBase.Api.Services;

namespace OpenGitBase.Api.Tests.Services;

public class StorageLfsClientTests
{
    private const string Oid = "4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393";
    private static readonly Guid RepositoryId = Guid.Parse("0f3c5a52-8f7e-4c55-9a3d-2b1e6f4d7c90");
    private static readonly StorageNodeEndpoint Node = new(Guid.NewGuid(), "storage-1", 8081, "node-token");

    [Fact]
    public async Task GetObjectSizeAsync_WhenTheNodeHoldsTheObject_ReturnsItsSize()
    {
        HttpRequestMessage? sent = null;
        var client = CreateClient(request =>
        {
            sent = request;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentLength = 5_000_000;
            return response;
        });

        var size = await client.GetObjectSizeAsync(Node, RepositoryId, Oid, CancellationToken.None);

        Assert.Equal(5_000_000, size);
        Assert.NotNull(sent);
        Assert.Equal(HttpMethod.Head, sent.Method);
        Assert.Equal(
            $"http://storage-1:8081/internal/lfs/{RepositoryId:D}/objects/{Oid}",
            sent.RequestUri?.ToString()
        );
        Assert.Equal("Bearer node-token", sent.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task GetObjectSizeAsync_WhenTheNodeLacksTheObject_ReturnsNull()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var size = await client.GetObjectSizeAsync(Node, RepositoryId, Oid, CancellationToken.None);

        Assert.Null(size);
    }

    [Fact]
    public async Task GetObjectSizeAsync_WhenTheNodeFails_Throws()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetObjectSizeAsync(Node, RepositoryId, Oid, CancellationToken.None)
        );
    }

    private static StorageLfsClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(respond(request));
    }
}
