using System.Net;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class DocsCommandTests : IDisposable
{
    private readonly List<string> _tempPaths = [];

    public void Dispose()
    {
        foreach (var path in _tempPaths)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Docs_pull_exports_matching_discussions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogb-docs-pull-{Guid.NewGuid():N}");
        _tempPaths.Add(tempDir);

        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            [{"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"[PRD] Mirror export","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}]
            """);
        handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"id":"11111111-1111-1111-1111-111111111111","number":42,"title":"[PRD] Mirror export","body":"# Exported","status":0,"createdAt":"2026-07-12T10:00:00Z","updatedAt":"2026-07-12T10:00:00Z","tags":[]}
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--hostname", "https://forge.example.com",
                "docs", "-R", "acme/demo", "pull", "--output-dir", tempDir,
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        var exportedPath = Path.Combine(tempDir, "docs", "prd", "mirror-export.md");
        Assert.True(File.Exists(exportedPath));
        Assert.Contains("Exported 1 file", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Docs_pull_json_outputs_inventory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogb-docs-pull-{Guid.NewGuid():N}");
        _tempPaths.Add(tempDir);

        var handler = new StubHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "[]");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await CliApp.RunAsync(
            [
                "--json",
                "--hostname", "https://forge.example.com",
                "docs", "-R", "acme/demo", "pull", "--output-dir", tempDir,
            ],
            output,
            error,
            CliTestSupport.CreateOverrides(handler, "https://forge.example.com"));

        Assert.Equal(0, exitCode);
        Assert.Contains("\"files\"", output.ToString(), StringComparison.Ordinal);
    }
}
