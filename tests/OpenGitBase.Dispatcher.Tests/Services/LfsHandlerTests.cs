using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

/// <summary>Git LFS through the dispatcher's single HTTP entry point, against a stub API and stub storage.</summary>
public class LfsHandlerTests
{
    private const string Oid = "4d7a214614ab2935c943f9e0ff69d22eadbb8f32b1258daaa5e2ca24d17e2393";
    private const string RepositoryId = "0f3c5a52-8f7e-4c55-9a3d-2b1e6f4d7c90";
    private const string LfsContentType = "application/vnd.git-lfs+json";

    [Fact]
    public async Task Batch_ForwardsCredentialsAndObjectsAndRelaysTheApiAnswer()
    {
        var harness = new Harness();
        harness.ApiAnswers["batch"] = (200, """{"transfer":"basic","objects":[]}""");
        var context = CreateContext(
            "POST",
            "/alice/demo.git/info/lfs/objects/batch",
            """{"operation":"upload","transfers":["basic"],"objects":[{"oid":"__OID__","size":12}],"hash_algo":"sha256"}"""
                .Replace("__OID__", Oid, StringComparison.Ordinal)
        );
        context.Request.Headers.Authorization = Basic("alice", "ogb_pat");
        context.Request.Host = new HostString("git.example.com");
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(LfsContentType, context.Response.ContentType);
        Assert.Equal("""{"transfer":"basic","objects":[]}""", ResponseText(context));
        var forwarded = harness.ApiRequests["batch"];
        Assert.Equal("ogb_pat", forwarded.GetProperty("accessToken").GetString());
        Assert.Equal("alice/demo", forwarded.GetProperty("repositoryPath").GetString());
        Assert.Equal("upload", forwarded.GetProperty("operation").GetString());
        Assert.Equal("sha256", forwarded.GetProperty("hashAlgo").GetString());
        Assert.Equal(Oid, forwarded.GetProperty("objects")[0].GetProperty("oid").GetString());
        Assert.Equal(12, forwarded.GetProperty("objects")[0].GetProperty("size").GetInt64());
        Assert.Equal(
            "https://git.example.com/alice/demo.git/info/lfs",
            forwarded.GetProperty("lfsBaseUrl").GetString()
        );
    }

    [Fact]
    public async Task Batch_WhenTheApiAsksForCredentials_AddsTheBasicChallenge()
    {
        var harness = new Harness();
        harness.ApiAnswers["batch"] = (401, """{"message":"Credentials needed."}""");
        var context = CreateContext("POST", "/alice/demo.git/info/lfs/objects/batch", """{"operation":"download","objects":[]}""");

        await harness.Handler.HandleAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("Basic realm=\"OpenGitBase\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task Batch_WhenLfsIsDisabled_RelaysThe501()
    {
        var harness = new Harness();
        harness.ApiAnswers["batch"] = (501, """{"message":"Git LFS is not enabled on this server."}""");
        var context = CreateContext("POST", "/alice/demo.git/info/lfs/objects/batch", """{"operation":"download","objects":[]}""");
        context.Request.Headers.Authorization = Basic("alice", "ogb_pat");

        await harness.Handler.HandleAsync(context);

        Assert.Equal(501, context.Response.StatusCode);
        Assert.Equal(LfsContentType, context.Response.ContentType);
        Assert.Contains("not enabled", ResponseText(context), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Batch_WithMalformedJson_Answers422WithoutCallingTheApi()
    {
        var harness = new Harness();
        var context = CreateContext("POST", "/alice/demo.git/info/lfs/objects/batch", "{not json");

        await harness.Handler.HandleAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        Assert.Empty(harness.ApiRequests);
    }

    [Fact]
    public async Task Upload_StreamsTheBodyToThePrimaryWithTheBodyLimitAtTheDeclaredSize()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12);
        var body = new NonSeekableStream(Encoding.ASCII.GetBytes("hello world!"));
        var context = CreateContext("PUT", $"/alice/demo.git/info/lfs/objects/{Oid}", body);
        context.Request.Headers.Authorization = "Bearer action-token";
        context.Request.ContentLength = 12;
        var bodySize = new BodySizeFeature { MaxRequestBodySize = 30_000_000 };
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySize);

        await harness.Handler.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(12, bodySize.MaxRequestBodySize);
        Assert.Same(body, context.Request.Body);
        var transferCheck = harness.ApiRequests["transfer-check"];
        Assert.Equal("action-token", transferCheck.GetProperty("token").GetString());
        Assert.Equal("upload", transferCheck.GetProperty("operation").GetString());
        Assert.Equal(Oid, transferCheck.GetProperty("oid").GetString());
        Assert.Equal("alice/demo", transferCheck.GetProperty("repositoryPath").GetString());
        var stored = Assert.Single(harness.StorageRequests);
        Assert.Equal(HttpMethod.Put, stored.Method);
        Assert.Equal($"http://storage-1:8082/lfs/{RepositoryId}/objects/{Oid}", stored.Uri);
        Assert.Equal(12, stored.ContentLength);
        Assert.Equal("hello world!", Encoding.ASCII.GetString(stored.Body));
    }

    [Fact]
    public async Task Upload_WhenContentLengthDiffersFromTheDeclaredSize_Answers422WithoutTouchingStorage()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12);
        var context = CreateContext("PUT", $"/alice/demo.git/info/lfs/objects/{Oid}", "hello world!!!");
        context.Request.Headers.Authorization = "Bearer action-token";
        context.Request.ContentLength = 14;

        await harness.Handler.HandleAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        Assert.Empty(harness.StorageRequests);
    }

    [Fact]
    public async Task Upload_WithoutContentLength_Answers411()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12);
        var context = CreateContext("PUT", $"/alice/demo.git/info/lfs/objects/{Oid}", "hello world!");
        context.Request.Headers.Authorization = "Bearer action-token";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(411, context.Response.StatusCode);
        Assert.Empty(harness.StorageRequests);
    }

    [Fact]
    public async Task Upload_WhenTheApiRejectsTheToken_RelaysTheRefusal()
    {
        var harness = new Harness();
        harness.ApiAnswers["transfer-check"] = (401, """{"message":"The LFS action token has expired; run the command again."}""");
        var context = CreateContext("PUT", $"/alice/demo.git/info/lfs/objects/{Oid}", "hello world!");
        context.Request.Headers.Authorization = "Bearer action-token";
        context.Request.ContentLength = 12;

        await harness.Handler.HandleAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Contains("expired", ResponseText(context), StringComparison.Ordinal);
        Assert.Empty(harness.StorageRequests);
    }

    [Fact]
    public async Task Upload_WithoutAnActionToken_Answers401()
    {
        var harness = new Harness();
        var context = CreateContext("PUT", $"/alice/demo.git/info/lfs/objects/{Oid}", "hello world!");
        context.Request.ContentLength = 12;

        await harness.Handler.HandleAsync(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Empty(harness.ApiRequests);
    }

    [Fact]
    public async Task Upload_WhenStorageRejectsTheContent_Answers422()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12);
        harness.StorageAnswer = _ => new HttpResponseMessage((HttpStatusCode)422)
        {
            Content = new StringContent("""{"error":"Content does not match the LFS oid."}"""),
        };
        var context = CreateContext("PUT", $"/alice/demo.git/info/lfs/objects/{Oid}", "hello world!");
        context.Request.Headers.Authorization = "Bearer action-token";
        context.Request.ContentLength = 12;

        await harness.Handler.HandleAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        Assert.Equal(LfsContentType, context.Response.ContentType);
    }

    [Fact]
    public async Task Download_StreamsTheObjectFromStorage()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12);
        harness.StorageAnswer = _ => Bytes("hello world!");
        var context = CreateContext("GET", $"/alice/demo.git/info/lfs/objects/{Oid}", string.Empty);
        context.Request.Headers.Authorization = "Bearer action-token";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/octet-stream", context.Response.ContentType);
        Assert.Equal(12, context.Response.ContentLength);
        Assert.Equal("hello world!", ResponseText(context));
        Assert.Equal("download", harness.ApiRequests["transfer-check"].GetProperty("operation").GetString());
        Assert.Equal(
            $"http://storage-1:8082/lfs/{RepositoryId}/objects/{Oid}",
            Assert.Single(harness.StorageRequests).Uri
        );
    }

    [Fact]
    public async Task Download_WhenTheFirstTargetLacksTheObject_UsesTheNext()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12, hosts: ["storage-1", "storage-2"]);
        harness.StorageAnswer = request =>
            request.RequestUri!.Host == "storage-1" ? new HttpResponseMessage(HttpStatusCode.NotFound) : Bytes("hello world!");
        var context = CreateContext("GET", $"/alice/demo.git/info/lfs/objects/{Oid}", string.Empty);
        context.Request.Headers.Authorization = "Bearer action-token";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("hello world!", ResponseText(context));
        Assert.Equal(2, harness.StorageRequests.Count);
    }

    [Fact]
    public async Task Download_WhenNoTargetHoldsTheObject_Answers404()
    {
        var harness = new Harness();
        harness.AnswerTransferCheck(size: 12);
        harness.StorageAnswer = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        var context = CreateContext("GET", $"/alice/demo.git/info/lfs/objects/{Oid}", string.Empty);
        context.Request.Headers.Authorization = "Bearer action-token";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(LfsContentType, context.Response.ContentType);
    }

    [Fact]
    public async Task Verify_ForwardsTheTokenAndObjectToTheApi()
    {
        var harness = new Harness();
        harness.ApiAnswers["verify"] = (200, "{}");
        var context = CreateContext(
            "POST",
            $"/alice/demo.git/info/lfs/objects/{Oid}/verify",
            $$"""{"oid":"{{Oid}}","size":12}"""
        );
        context.Request.Headers.Authorization = "Bearer action-token";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        var forwarded = harness.ApiRequests["verify"];
        Assert.Equal("action-token", forwarded.GetProperty("token").GetString());
        Assert.Equal(Oid, forwarded.GetProperty("oid").GetString());
        Assert.Equal(12, forwarded.GetProperty("size").GetInt64());
        Assert.Equal("alice/demo", forwarded.GetProperty("repositoryPath").GetString());
    }

    [Fact]
    public async Task Verify_WhenTheBodyNamesAnotherObject_Answers422()
    {
        var harness = new Harness();
        var context = CreateContext(
            "POST",
            $"/alice/demo.git/info/lfs/objects/{Oid}/verify",
            $$"""{"oid":"{{new string('0', 64)}}","size":12}"""
        );
        context.Request.Headers.Authorization = "Bearer action-token";

        await harness.Handler.HandleAsync(context);

        Assert.Equal(422, context.Response.StatusCode);
        Assert.Empty(harness.ApiRequests);
    }

    [Fact]
    public async Task LocksVerify_AnswersWithNoLocks()
    {
        var harness = new Harness();
        var context = CreateContext("POST", "/alice/demo.git/info/lfs/locks/verify", """{"ref":{"name":"refs/heads/main"}}""");

        await harness.Handler.HandleAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(LfsContentType, context.Response.ContentType);
        using var json = JsonDocument.Parse(ResponseText(context));
        Assert.Equal(0, json.RootElement.GetProperty("ours").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("theirs").GetArrayLength());
    }

    [Fact]
    public async Task UnsupportedLfsPath_Answers404InTheLfsFormat()
    {
        var harness = new Harness();
        var context = CreateContext("POST", "/alice/demo.git/info/lfs/locks", "{}");

        await harness.Handler.HandleAsync(context);

        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(LfsContentType, context.Response.ContentType);
    }

    private static DefaultHttpContext CreateContext(string method, string path, string body) =>
        CreateContext(method, path, new MemoryStream(Encoding.UTF8.GetBytes(body)));

    private static DefaultHttpContext CreateContext(string method, string path, Stream body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost:8089");
        context.Request.Body = body;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static string Basic(string user, string password) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));

    private static string ResponseText(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private static HttpResponseMessage Bytes(string text) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes(text)) };

    private sealed record StorageRequest(HttpMethod Method, string Uri, long? ContentLength, byte[] Body);

    private sealed class Harness
    {
        public Harness()
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new DispatcherOptions { ApiUrl = "http://api.test" }
            );
            var api = new HttpClient(new StubHandler(AnswerApiAsync));
            var storage = new HttpClient(new StubHandler(AnswerStorageAsync));
            Handler = new GitSmartHttpHandler(
                new GitSmartHttpPathParser(),
                new RepositoryAccessCheckClient(api, options),
                new GitHttpProxyService(storage),
                options,
                NullLogger<GitSmartHttpHandler>.Instance,
                new LfsPathParser(),
                new LfsHandler(new LfsApiClient(api, options), new LfsObjectTransferService(storage), NullLogger<LfsHandler>.Instance)
            );
        }

        public GitSmartHttpHandler Handler { get; }

        public Dictionary<string, (int Status, string Body)> ApiAnswers { get; } = new();

        public Dictionary<string, JsonElement> ApiRequests { get; } = new();

        public List<StorageRequest> StorageRequests { get; } = [];

        public Func<HttpRequestMessage, HttpResponseMessage> StorageAnswer { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };

        public void AnswerTransferCheck(long size, string[]? hosts = null)
        {
            var targets = (hosts ?? ["storage-1"]).Select(host =>
                new { internalHost = host, internalSshPort = 22, internalGitHttpPort = 8082, role = "Primary" }
            );
            ApiAnswers["transfer-check"] = (
                200,
                JsonSerializer.Serialize(new { repositoryId = RepositoryId, oid = Oid, size, targets })
            );
        }

        private async Task<HttpResponseMessage> AnswerApiAsync(HttpRequestMessage request)
        {
            var endpoint = request.RequestUri!.AbsolutePath.Replace("/api/v1/internal/lfs/", string.Empty, StringComparison.Ordinal);
            ApiRequests[endpoint] = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone();
            var (status, body) = ApiAnswers.TryGetValue(endpoint, out var answer) ? answer : (500, "{}");
            return new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }

        private async Task<HttpResponseMessage> AnswerStorageAsync(HttpRequestMessage request)
        {
            var body = request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync();
            StorageRequests.Add(
                new StorageRequest(request.Method, request.RequestUri!.ToString(), request.Content?.Headers.ContentLength, body)
            );
            return StorageAnswer(request);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => answer(request);
    }

    private sealed class BodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; }
    }

    /// <summary>Like Kestrel's request body: readable once, front to back.</summary>
    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;

        public override long Position
        {
            get => base.Position;
            set => throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc) => throw new NotSupportedException();
    }
}
