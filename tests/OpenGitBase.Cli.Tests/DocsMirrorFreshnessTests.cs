using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Commands;

namespace OpenGitBase.Cli.Tests;

public sealed class DocsMirrorFreshnessTests : IDisposable
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
    public void FindMirrorFilesMissingForgeMarker_FlagsUnmarkedFiles()
    {
        var root = CreateTempRepo();
        WriteMirrorFile(root, "docs/prd/marked.md", "<!-- forge: #1 -->\n\nBody");
        WriteMirrorFile(root, "docs/prd/unmarked.md", "# No marker");

        var missing = DocsMirrorFreshnessChecker.FindMirrorFilesMissingForgeMarker(root);

        Assert.Single(missing);
        Assert.Equal("docs/prd/unmarked.md", missing[0]);
    }

    [Fact]
    public void FindStaleMirrorPaths_FlagsMarkedFilesNotInPullInventory()
    {
        var root = CreateTempRepo();
        WriteMirrorFile(root, "docs/prd/current.md", "<!-- forge: #1 -->\n\nBody");
        WriteMirrorFile(root, "docs/prd/stale.md", "<!-- forge: #99 -->\n\nOld");

        var stale = DocsMirrorFreshnessChecker.FindStaleMirrorPaths(
            [new DocsPullFileModel { Number = 1, Path = "docs/prd/current.md", Title = "[PRD] Current" }],
            root);

        Assert.Single(stale);
        Assert.Equal("docs/prd/stale.md", stale[0]);
    }

    [Fact]
    public void Pull_into_existing_mirror_keeps_renamed_files_fresh()
    {
        var root = CreateTempRepo();
        WriteMirrorFile(root, "docs/prd/merge-requests.md", "<!-- forge: #18 -->\n\nOld");
        WriteMirrorFile(root, "docs/issues/merge-requests/04-git-push-enforcement.md", "<!-- forge: #164 -->\n\nOld");

        var pulled = DocsMirrorExporter.Export(
            [
                new DiscussionModel { Number = 18, Title = "[PRD] Merge Requests (Branch Protection, Review, and Server-Side Merge)", Body = "New" },
                new DiscussionModel { Number = 164, Title = "[slice] mr-04 — Git push enforcement", Body = "New" },
            ],
            root);

        Assert.Empty(DocsMirrorFreshnessChecker.FindStaleMirrorPaths(pulled, root));
        Assert.Empty(DocsMirrorFreshnessChecker.FindMirrorFilesMissingForgeMarker(root));
    }

    [Fact]
    public void FindStaleMirrorPaths_FlagsSecondFileWithSameMarker()
    {
        var root = CreateTempRepo();
        WriteMirrorFile(root, "docs/prd/a-merge-requests.md", "<!-- forge: #18 -->\n\nFirst");
        WriteMirrorFile(root, "docs/prd/b-merge-requests.md", "<!-- forge: #18 -->\n\nSecond");

        var pulled = DocsMirrorExporter.Export(
            [new DiscussionModel { Number = 18, Title = "[PRD] Merge Requests", Body = "New" }],
            root);

        Assert.Equal("docs/prd/a-merge-requests.md", Assert.Single(pulled).Path);
        Assert.Equal(["docs/prd/b-merge-requests.md"], DocsMirrorFreshnessChecker.FindStaleMirrorPaths(pulled, root));
    }

    private static void WriteMirrorFile(string root, string relativePath, string content)
    {
        var absolutePath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, content);
    }

    private string CreateTempRepo()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogb-mirror-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }
}
