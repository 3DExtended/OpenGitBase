using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Commands;

namespace OpenGitBase.Cli.Tests;

public sealed class DocsMirrorExporterTests : IDisposable
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

    [Theory]
    [InlineData("[PRD] ogb docs pull", "docs/prd/ogb-docs-pull.md")]
    [InlineData("[ADR] 0005 — Discussion links", "docs/adr/0005-discussion-links.md")]
    [InlineData("[slice] mr-01 — API client", "docs/issues/mr-01.md")]
    public void ResolveRelativePath_MapsTitlePrefixes(string title, string expectedPath)
    {
        var path = DocsMirrorExporter.ResolveRelativePath(title);
        Assert.Equal(expectedPath, path.Replace('\\', '/'));
    }

    [Fact]
    public void BuildFileContent_IncludesForgeMarker()
    {
        var content = DocsMirrorExporter.BuildFileContent(
            new DiscussionModel
            {
                Number = 42,
                Title = "[PRD] Spec",
                Body = "# Spec body",
            });

        Assert.Contains("<!-- forge: #42 -->", content, StringComparison.Ordinal);
        Assert.Contains("# Spec body", content, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExportFile_WritesMarkdownUnderOutputRoot()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ogb-docs-pull-{Guid.NewGuid():N}");
        _tempPaths.Add(tempDir);

        var file = DocsMirrorExporter.BuildExportFile(
            new DiscussionModel
            {
                Number = 7,
                Title = "[PRD] Mirror export",
                Body = "Body text",
            },
            tempDir);

        Assert.Equal("docs/prd/mirror-export.md", file.Path);
        var absolutePath = Path.Combine(tempDir, file.Path);
        Assert.True(File.Exists(absolutePath));
        Assert.Contains("<!-- forge: #7 -->", File.ReadAllText(absolutePath), StringComparison.Ordinal);
    }
}
