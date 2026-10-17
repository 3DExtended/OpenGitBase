using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using OpenGitBase.ComputeAgent;

namespace OpenGitBase.Api.Tests.ComputeAgent;

public sealed class JobWorkspaceMaterializerTests : IDisposable
{
    private const string JobToken = "job-identity-token";

    private static readonly Guid JobId = Guid.NewGuid();

    private readonly string _projectDir = Path.Combine(
        Path.GetTempPath(),
        $"ogb-workspace-test-{Guid.NewGuid():N}",
        "repo"
    );

    public JobWorkspaceMaterializerTests() => Directory.CreateDirectory(_projectDir);

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_projectDir)!, recursive: true);

    [Fact]
    public async Task MaterializeAsync_UnpacksTheArchiveTheApiServesForTheJob()
    {
        HttpRequestMessage? sent = null;
        var archive = BuildArchive(writer =>
        {
            // `git archive` opens with a pax global header holding the commit id.
            writer.WriteEntry(
                new PaxGlobalExtendedAttributesTarEntry(
                    new Dictionary<string, string> { ["comment"] = "3f786850e387550fdab836ed7e6dc881de23001b" }
                )
            );
            writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, "scripts/"));
            WriteFile(writer, "README.md", "# repo\n", UnixFileMode.UserRead | UnixFileMode.UserWrite);
            WriteFile(
                writer,
                "scripts/test.sh",
                "#!/bin/sh\necho ok\n",
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            );
        });

        var result = await JobWorkspaceMaterializer.MaterializeAsync(
            CreateClient(request =>
            {
                sent = request;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) };
            }),
            JobId,
            JobToken,
            _projectDir,
            CancellationToken.None
        );

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal($"http://api.test/pipeline/jobs/{JobId}/workspace-archive", sent?.RequestUri?.AbsoluteUri);
        Assert.Equal($"Bearer {JobToken}", sent?.Headers.Authorization?.ToString());
        Assert.Equal("# repo\n", await File.ReadAllTextAsync(Path.Combine(_projectDir, "README.md")));
        if (!OperatingSystem.IsWindows())
        {
            // Scripts in the repository must stay runnable, e.g. `./scripts/test.sh`.
            Assert.True(
                File.GetUnixFileMode(Path.Combine(_projectDir, "scripts", "test.sh")).HasFlag(UnixFileMode.UserExecute)
            );
        }
    }

    // Each of these used to leave the job running its script in an empty directory.
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task MaterializeAsync_WhenTheApiAnswersAnError_Fails(HttpStatusCode status)
    {
        var result = await JobWorkspaceMaterializer.MaterializeAsync(
            CreateClient(_ => new HttpResponseMessage(status)),
            JobId,
            JobToken,
            _projectDir,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Contains(((int)status).ToString(), result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaterializeAsync_WhenTheApiIsUnreachable_Fails()
    {
        var result = await JobWorkspaceMaterializer.MaterializeAsync(
            CreateClient(_ => throw new HttpRequestException("Connection refused")),
            JobId,
            JobToken,
            _projectDir,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.Contains("Connection refused", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaterializeAsync_WhenTheArchiveIsNotGzip_Fails()
    {
        var result = await JobWorkspaceMaterializer.MaterializeAsync(
            CreateClient(_ =>
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>oops</html>") }
            ),
            JobId,
            JobToken,
            _projectDir,
            CancellationToken.None
        );

        Assert.False(result.Success);
    }

    [Fact]
    public async Task MaterializeAsync_WhenAnEntryLeavesTheWorkspace_FailsWithoutWritingIt()
    {
        var archive = BuildArchive(writer =>
            WriteFile(writer, "../escaped.txt", "nope", UnixFileMode.UserRead | UnixFileMode.UserWrite)
        );

        var result = await JobWorkspaceMaterializer.MaterializeAsync(
            CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(archive) }),
            JobId,
            JobToken,
            _projectDir,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_projectDir)!, "escaped.txt")));
    }

    [Fact]
    public async Task MaterializeAsync_WithoutAJobToken_FailsWithoutCallingTheApi()
    {
        var called = false;

        var result = await JobWorkspaceMaterializer.MaterializeAsync(
            CreateClient(_ =>
            {
                called = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
            JobId,
            string.Empty,
            _projectDir,
            CancellationToken.None
        );

        Assert.False(result.Success);
        Assert.False(called);
    }

    private static byte[] BuildArchive(Action<TarWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            write(writer);
        }

        return buffer.ToArray();
    }

    private static void WriteFile(TarWriter writer, string name, string content, UnixFileMode mode) =>
        writer.WriteEntry(
            new PaxTarEntry(TarEntryType.RegularFile, name)
            {
                Mode = mode,
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
            }
        );

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler(respond)) { BaseAddress = new Uri("http://api.test/") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(respond(request));
    }
}
