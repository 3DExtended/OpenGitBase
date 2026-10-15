using System.Net;
using System.Text;
using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Contracts;

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

        var size = await client.GetObjectSizeAsync(Node, RepositoryId, Oid, LfsObjectCopyKind.Plaintext, CancellationToken.None);

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

        var size = await client.GetObjectSizeAsync(Node, RepositoryId, Oid, LfsObjectCopyKind.Plaintext, CancellationToken.None);

        Assert.Null(size);
    }

    [Fact]
    public async Task GetObjectSizeAsync_WhenTheNodeFails_Throws()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetObjectSizeAsync(Node, RepositoryId, Oid, LfsObjectCopyKind.Plaintext, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetObjectSizeAsync_ForAnEncryptedCopy_AsksTheEncryptedRoute()
    {
        HttpRequestMessage? sent = null;
        var client = CreateClient(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await client.GetObjectSizeAsync(Node, RepositoryId, Oid, LfsObjectCopyKind.Encrypted, CancellationToken.None);

        Assert.Equal(
            $"http://storage-1:8081/internal/lfs/{RepositoryId:D}/encrypted/{Oid}",
            sent?.RequestUri?.ToString()
        );
    }

    [Fact]
    public async Task OpenObjectAsync_StreamsTheObject()
    {
        HttpRequestMessage? sent = null;
        var client = CreateClient(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("content"u8.ToArray()) };
        });

        await using var stream = await client.OpenObjectAsync(
            Node,
            RepositoryId,
            Oid,
            LfsObjectCopyKind.Plaintext,
            CancellationToken.None
        );

        Assert.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal("content", await reader.ReadToEndAsync());
        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("Bearer node-token", sent?.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task OpenObjectAsync_WhenTheNodeLacksTheObject_ReturnsNull()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var stream = await client.OpenObjectAsync(Node, RepositoryId, Oid, LfsObjectCopyKind.Plaintext, CancellationToken.None);

        Assert.Null(stream);
    }

    [Fact]
    public async Task PutObjectAsync_SendsTheContentWithItsLength()
    {
        HttpRequestMessage? sent = null;
        byte[]? body = null;
        var client = CreateClient(request =>
        {
            sent = request;
            body = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        await client.PutObjectAsync(
            Node,
            RepositoryId,
            Oid,
            LfsObjectCopyKind.Encrypted,
            new ByteArrayContent([1, 2, 3]),
            CancellationToken.None
        );

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal($"http://storage-1:8081/internal/lfs/{RepositoryId:D}/encrypted/{Oid}", sent?.RequestUri?.ToString());
        Assert.Equal(3, sent?.Content?.Headers.ContentLength);
        Assert.Equal([1, 2, 3], body);
    }

    [Fact]
    public async Task PutObjectAsync_WhenTheNodeRefuses_Throws()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.PutObjectAsync(
                Node,
                RepositoryId,
                Oid,
                LfsObjectCopyKind.Plaintext,
                new ByteArrayContent([1]),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task GetInventoryAsync_ReturnsTheKindAndTheObjectsTheNodeHolds()
    {
        HttpRequestMessage? sent = null;
        var client = CreateClient(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"kind":"encrypted","objects":[{"oid":"{{Oid}}","size":1077}]}""",
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        });

        var inventory = await client.GetInventoryAsync(Node, RepositoryId, CancellationToken.None);

        Assert.Equal(LfsObjectCopyKind.Encrypted, inventory.Kind);
        Assert.Equal(new Dictionary<string, long> { [Oid] = 1077 }, inventory.Objects);
        Assert.Equal(HttpMethod.Get, sent!.Method);
        Assert.Equal($"http://storage-1:8081/internal/lfs/{RepositoryId:D}/inventory", sent.RequestUri?.ToString());
        Assert.Equal("Bearer node-token", sent.Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task GetInventoryAsync_OfAPlaintextNode_ReportsPlaintext()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"kind":"plaintext","objects":[]}""", Encoding.UTF8, "application/json"),
        });

        var inventory = await client.GetInventoryAsync(Node, RepositoryId, CancellationToken.None);

        Assert.Equal(LfsObjectCopyKind.Plaintext, inventory.Kind);
        Assert.Empty(inventory.Objects);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, """{"kind":"unknown","objects":[]}""")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task GetInventoryAsync_WhenTheNodeCannotAnswer_Throws(HttpStatusCode status, string body)
    {
        var client = CreateClient(_ => new HttpResponseMessage(status) { Content = new StringContent(body) });

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetInventoryAsync(Node, RepositoryId, CancellationToken.None));
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
