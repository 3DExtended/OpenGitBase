using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitSmartHttpHandlerTests
{
    private const long KestrelDefaultMaxRequestBodySize = 30_000_000;

    [Fact]
    public async Task HandleAsync_RaisesTheRequestBodyLimitToMaxPushBytesForReceivePack()
    {
        var harness = new Harness(maxPushBytes: 1_073_741_824);
        var bodySizeFeature = new BodySizeFeature { MaxRequestBodySize = KestrelDefaultMaxRequestBodySize };
        var context = CreateReceivePackContext(BuildReceivePackBody(packBytes: 1024), bodySizeFeature);

        await harness.Handler.HandleAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1_073_741_824, bodySizeFeature.MaxRequestBodySize);
    }

    [Fact]
    public async Task HandleAsync_LeavesTheRequestBodyLimitAloneForUploadPack()
    {
        var harness = new Harness(maxPushBytes: 1_073_741_824);
        var bodySizeFeature = new BodySizeFeature { MaxRequestBodySize = KestrelDefaultMaxRequestBodySize };
        var context = CreateContext("/alice/demo.git/git-upload-pack", new MemoryStream([0x30, 0x30, 0x30, 0x30]));
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySizeFeature);

        await harness.Handler.HandleAsync(context);

        Assert.Equal(KestrelDefaultMaxRequestBodySize, bodySizeFeature.MaxRequestBodySize);
    }

    [Fact]
    public async Task HandleAsync_Returns413WhenThePushExceedsTheBodyLimit()
    {
        var harness = new Harness(maxPushBytes: 1_073_741_824);
        var context = CreateReceivePackContext(new TooLargeBodyStream(), new BodySizeFeature());

        await harness.Handler.HandleAsync(context);

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);
        Assert.Equal(0, harness.AccessChecks);
        Assert.Null(harness.StorageBody);
    }

    [Fact]
    public async Task HandleAsync_ForwardsTheWholePushAndReportsItsSize()
    {
        var harness = new Harness(maxPushBytes: 1_073_741_824);
        var body = BuildReceivePackBody(packBytes: 3 * 1024 * 1024);
        var context = CreateReceivePackContext(body, new BodySizeFeature());

        await harness.Handler.HandleAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(body.Length, harness.ReportedPackSizeBytes);
        Assert.Equal(body, harness.StorageBody);
        Assert.Equal("refs/heads/main", harness.ReportedRefName);
    }

    private static DefaultHttpContext CreateReceivePackContext(byte[] body, BodySizeFeature bodySizeFeature) =>
        CreateReceivePackContext(new MemoryStream(body), bodySizeFeature);

    private static DefaultHttpContext CreateReceivePackContext(Stream body, BodySizeFeature bodySizeFeature)
    {
        var context = CreateContext("/alice/demo.git/git-receive-pack", body);
        context.Request.ContentType = "application/x-git-receive-pack-request";
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySizeFeature);
        return context;
    }

    private static DefaultHttpContext CreateContext(string path, Stream body)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Headers.Authorization =
            "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:ogb_test_token"));
        context.Request.Body = body;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static byte[] BuildReceivePackBody(int packBytes)
    {
        var command =
            "0000000000000000000000000000000000000000 "
            + "2222222222222222222222222222222222222222 "
            + "refs/heads/main\0 report-status\n";
        var pktLine = Encoding.ASCII.GetBytes((command.Length + 4).ToString("x4") + command);
        var pack = new byte[packBytes];
        new Random(413).NextBytes(pack);
        Encoding.ASCII.GetBytes("PACK").CopyTo(pack, 0);
        return [.. pktLine, .. Encoding.ASCII.GetBytes("0000"), .. pack];
    }

    private sealed class Harness
    {
        public Harness(long maxPushBytes)
        {
            var options = Microsoft.Extensions.Options.Options.Create(
                new DispatcherOptions { ApiUrl = "http://api.test", MaxPushBytes = maxPushBytes }
            );
            var api = new HttpClient(new StubHandler(AnswerAccessCheckAsync));
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

        public int AccessChecks { get; private set; }

        public long? ReportedPackSizeBytes { get; private set; }

        public string? ReportedRefName { get; private set; }

        public byte[]? StorageBody { get; private set; }

        private async Task<HttpResponseMessage> AnswerAccessCheckAsync(HttpRequestMessage request)
        {
            AccessChecks++;
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("packSizeBytes", out var packSize))
            {
                ReportedPackSizeBytes = packSize.GetInt64();
            }

            if (json.RootElement.TryGetProperty("refUpdates", out var refUpdates)
                && refUpdates.GetArrayLength() > 0)
            {
                ReportedRefName = refUpdates[0].GetProperty("refName").GetString();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "allowed": true,
                      "physicalPath": "/srv/git/demo-id.git",
                      "storageNodeInternalHost": "storage-1",
                      "storageNodeInternalSshPort": 22,
                      "storageNodeInternalGitHttpPort": 8082
                    }
                    """,
                    Encoding.UTF8,
                    "application/json"
                ),
            };
        }

        private async Task<HttpResponseMessage> AnswerStorageAsync(HttpRequestMessage request)
        {
            if (request.Content is not null)
            {
                StorageBody = await request.Content.ReadAsByteArrayAsync();
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> answer)
        : HttpMessageHandler
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

    /// <summary>What Kestrel's request body does once a push passes MaxRequestBodySize.</summary>
    private sealed class TooLargeBodyStream : MemoryStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw TooLarge();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw TooLarge();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            throw TooLarge();

        private static BadHttpRequestException TooLarge() =>
            new(
                $"Request body too large. The max request body size is {KestrelDefaultMaxRequestBodySize} bytes.",
                StatusCodes.Status413PayloadTooLarge
            );
    }
}
