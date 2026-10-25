using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using OpenGitBase.Dispatcher.Options;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.RealGit;

/// <summary>The dispatcher's Smart HTTP edge on real Kestrel, with the API's access check stubbed to allow.</summary>
internal sealed class DispatcherHttpHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private DispatcherHttpHost(WebApplication app, int port, ConcurrentQueue<string> uploadPackEncodings)
    {
        _app = app;
        Port = port;
        UploadPackContentEncodings = uploadPackEncodings;
    }

    public int Port { get; }

    /// <summary>The Content-Encoding of every upload-pack POST the git client sent ("" when none).</summary>
    public ConcurrentQueue<string> UploadPackContentEncodings { get; }

    public static async Task<DispatcherHttpHost> StartAsync(int storagePort, string physicalPath)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new DispatcherOptions { ApiUrl = "http://api.test" }
        );
        var api = new HttpClient(new AllowingAccessCheckHandler(storagePort, physicalPath));
        var storage = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var handler = new GitSmartHttpHandler(
            new GitSmartHttpPathParser(),
            new RepositoryAccessCheckClient(api, options),
            new GitHttpProxyService(storage),
            options,
            NullLogger<GitSmartHttpHandler>.Instance,
            new LfsPathParser(),
            new LfsHandler(
                new LfsApiClient(api, options),
                new LfsObjectTransferService(storage),
                NullLogger<LfsHandler>.Instance
            )
        );

        var uploadPackEncodings = new ConcurrentQueue<string>();
        var app = TestKestrel.Build();
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method)
                && context.Request.Path.Value!.EndsWith("/git-upload-pack", StringComparison.Ordinal))
            {
                uploadPackEncodings.Enqueue(context.Request.Headers.ContentEncoding.ToString());
            }

            await next(context);
        });
        app.Run(handler.HandleAsync);
        return new DispatcherHttpHost(app, await TestKestrel.StartAsync(app), uploadPackEncodings);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private sealed class AllowingAccessCheckHandler(int storagePort, string physicalPath) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var target = $$"""{ "internalHost": "127.0.0.1", "internalSshPort": 22, "internalGitHttpPort": {{storagePort}}, "role": "Primary" }""";
            var json = $$"""
                {
                  "allowed": true,
                  "physicalPath": "{{physicalPath}}",
                  "primary": {{target}},
                  "readTargets": [{{target}}]
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
