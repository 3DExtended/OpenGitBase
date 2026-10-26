using System.Net;
using Microsoft.AspNetCore.Http;
using OpenGitBase.Dispatcher.Models;
using OpenGitBase.Dispatcher.Services;

namespace OpenGitBase.Dispatcher.Tests.Services;

public class GitHttpProxyServiceTests
{
    [Fact]
    public void BuildStorageRelativePath_MapsPhysicalPathToGitHttpPath()
    {
        var gitRequest = new GitSmartHttpRequest
        {
            GitSuffix = "info/refs",
            Operation = RepositoryOperation.ReadGit,
            RepositoryPath = "alice/demo",
        };

        var relativePath = GitHttpProxyService.BuildStorageRelativePath(
            "/srv/git/71a7e007-c34e-4c09-9329-9e664b1b1708.git",
            gitRequest,
            new QueryString("?service=git-upload-pack")
        );

        Assert.Equal(
            "/71a7e007-c34e-4c09-9329-9e664b1b1708.git/info/refs?service=git-upload-pack",
            relativePath
        );
    }

    [Fact]
    public void BuildStorageRelativePath_MapsUploadPackPath()
    {
        var gitRequest = new GitSmartHttpRequest
        {
            GitSuffix = "git-upload-pack",
            Operation = RepositoryOperation.ReadGit,
            RepositoryPath = "alice/demo",
        };

        var relativePath = GitHttpProxyService.BuildStorageRelativePath(
            "/srv/git/demo-id.git",
            gitRequest,
            QueryString.Empty
        );

        Assert.Equal("/demo-id.git/git-upload-pack", relativePath);
    }

    /// <summary>
    /// Git gzips an upload-pack request above 1 KiB. Without the header git-http-backend hands the
    /// compressed bytes to upload-pack, which reads them as a pkt-line length and dies: the client
    /// gets an empty answer ("expected 'acknowledgments'").
    /// </summary>
    [Fact]
    public async Task ProxyAsync_ForwardsTheRequestContentEncoding()
    {
        string[] forwardedEncoding = [];
        var storage = new HttpClient(new StubHandler(request =>
        {
            forwardedEncoding = request.Content?.Headers.ContentEncoding.ToArray() ?? [];
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        }));
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-git-upload-pack-request";
        context.Request.Headers.ContentEncoding = "gzip";
        context.Request.Body = new MemoryStream([0x1f, 0x8b, 0x08, 0x00]);
        context.Response.Body = new MemoryStream();

        await new GitHttpProxyService(storage).ProxyAsync(
            context,
            new RepositoryAccessCheckResponse
            {
                Allowed = true,
                PhysicalPath = "/srv/git/demo-id.git",
                Primary = new StorageRoutingTarget { InternalHost = "storage-1", InternalGitHttpPort = 8082 },
            },
            new GitSmartHttpRequest
            {
                GitSuffix = "git-upload-pack",
                Operation = RepositoryOperation.ReadGit,
                RepositoryPath = "alice/demo",
            },
            CancellationToken.None
        );

        Assert.Equal(["gzip"], forwardedEncoding);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(answer(request));
    }
}
