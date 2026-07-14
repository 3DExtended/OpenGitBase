using System.Net;
using System.Text.Json;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class DocsPullJsonGoldenTests
{
    [Fact]
    public async Task Docs_pull_json_matches_golden_inventory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogb-docs-golden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
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

            var actual = JsonDocument.Parse(output.ToString().Trim());
            var goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", "docs-pull.empty.json");
            var expected = JsonDocument.Parse(await File.ReadAllTextAsync(goldenPath));

            Assert.Equal(
                JsonSerializer.Serialize(expected, new JsonSerializerOptions { WriteIndented = true }),
                JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
