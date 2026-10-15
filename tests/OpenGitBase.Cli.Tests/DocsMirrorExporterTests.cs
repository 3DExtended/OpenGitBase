using OpenGitBase.Cli.Api.Models;
using OpenGitBase.Cli.Commands;

namespace OpenGitBase.Cli.Tests;

public sealed class DocsMirrorExporterTests : IDisposable
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 7, 15, 12, 21, 42, TimeSpan.Zero);

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
    public void BuildFileContent_AppendsContinuationCommentsAndSkipsOpeningCommentEcho()
    {
        var content = DocsMirrorExporter.BuildFileContent(
            Discussion(
                18,
                "[PRD] Long spec",
                body: "Part one",
                Comment("Part one", secondsAfterCreate: 1),
                Comment("Part two", secondsAfterCreate: 2),
                Comment("Part three\n", secondsAfterCreate: 4)));

        Assert.Equal("<!-- forge: #18 -->\n\nPart one\n\nPart two\n\nPart three\n", content);
    }

    [Fact]
    public void BuildFileContent_StopsAtFirstGapLongerThanPublishBurst()
    {
        var content = DocsMirrorExporter.BuildFileContent(
            Discussion(
                220,
                "[PRD] Status outage windows",
                body: null,
                Comment("Opening post", secondsAfterCreate: 1),
                Comment("All child slices implemented on main.", secondsAfterCreate: 4246),
                Comment("Late follow-up", secondsAfterCreate: 4250)));

        Assert.Equal("<!-- forge: #220 -->\n\nOpening post\n", content);
    }

    [Fact]
    public void BuildFileContent_StopsAtFirstCommentByAnotherAuthor()
    {
        var content = DocsMirrorExporter.BuildFileContent(
            Discussion(
                7,
                "[PRD] Spec",
                body: "Part one",
                Comment("Part one", secondsAfterCreate: 1),
                Comment("Looks good", secondsAfterCreate: 2, author: "reviewer"),
                Comment("Part two", secondsAfterCreate: 3)));

        Assert.Equal("<!-- forge: #7 -->\n\nPart one\n", content);
    }

    [Fact]
    public void BuildFileContent_IgnoresReplies()
    {
        var content = DocsMirrorExporter.BuildFileContent(
            Discussion(
                9,
                "[PRD] Spec",
                body: "Part one",
                Comment("Part one", secondsAfterCreate: 1),
                Comment("Orphaned reply", secondsAfterCreate: 2, parentCommentId: Guid.NewGuid()),
                Comment("Part two", secondsAfterCreate: 3)));

        Assert.Equal("<!-- forge: #9 -->\n\nPart one\n\nPart two\n", content);
    }

    [Fact]
    public void BuildFileContent_KeepsOpeningCommentThatDiffersFromBody()
    {
        var content = DocsMirrorExporter.BuildFileContent(
            Discussion(
                11,
                "[PRD] Spec",
                body: "Summary",
                Comment("Details", secondsAfterCreate: 1)));

        Assert.Equal("<!-- forge: #11 -->\n\nSummary\n\nDetails\n", content);
    }

    [Fact]
    public void BuildFileContent_PrefersEditedOpeningCommentOverStaleBody()
    {
        var editedOpening = Comment("Part one, fixed", secondsAfterCreate: 1);
        editedOpening.EditedAt = CreatedAt.AddDays(3);

        var content = DocsMirrorExporter.BuildFileContent(
            Discussion(
                12,
                "[PRD] Spec",
                body: "Part one, with typo",
                editedOpening,
                Comment("Part two", secondsAfterCreate: 2)));

        Assert.Equal("<!-- forge: #12 -->\n\nPart one, fixed\n\nPart two\n", content);
    }

    [Fact]
    public void BuildFileContent_FallsBackToTitleWithoutBodyOrComments()
    {
        var content = DocsMirrorExporter.BuildFileContent(Discussion(219, "[PRD] Empty", body: null));

        Assert.Equal("<!-- forge: #219 -->\n\n[PRD] Empty\n", content);
    }

    [Fact]
    public void Export_WritesMarkdownUnderOutputRoot()
    {
        var root = CreateTempRoot();

        var files = DocsMirrorExporter.Export([Discussion(7, "[PRD] Mirror export", body: "Body text")], root);

        var file = Assert.Single(files);
        Assert.Equal("docs/prd/mirror-export.md", file.Path);
        Assert.Equal("<!-- forge: #7 -->\n\nBody text\n", ReadMirrorFile(root, file.Path));
    }

    [Fact]
    public void Export_WritesUtf8WithoutByteOrderMark()
    {
        var root = CreateTempRoot();

        var file = Assert.Single(DocsMirrorExporter.Export([Discussion(7, "[PRD] Spec", body: "Body")], root));

        var bytes = File.ReadAllBytes(Path.Combine(root, file.Path));
        Assert.Equal((byte)'<', bytes[0]);
    }

    [Fact]
    public void Export_UpdatesExistingFileWithMatchingForgeMarker()
    {
        var root = CreateTempRoot();
        WriteMirrorFile(root, "docs/prd/merge-requests.md", "<!-- forge: #18 -->\n\nStale text\n");

        var files = DocsMirrorExporter.Export(
            [Discussion(18, "[PRD] Merge Requests (Branch Protection, Review, and Server-Side Merge)", body: "Fresh text")],
            root);

        Assert.Equal("docs/prd/merge-requests.md", Assert.Single(files).Path);
        Assert.Equal("<!-- forge: #18 -->\n\nFresh text\n", ReadMirrorFile(root, "docs/prd/merge-requests.md"));
        Assert.Single(Directory.GetFiles(Path.Combine(root, "docs", "prd")));
    }

    [Fact]
    public void Export_FindsMarkedFileInNestedFolder()
    {
        var root = CreateTempRoot();
        WriteMirrorFile(root, "docs/issues/merge-requests/04-git-push-enforcement.md", "<!-- forge: #164 -->\n\nOld\n");

        var files = DocsMirrorExporter.Export(
            [Discussion(164, "[slice] mr-04 — Git push enforcement", body: "New")],
            root);

        Assert.Equal("docs/issues/merge-requests/04-git-push-enforcement.md", Assert.Single(files).Path);
        Assert.False(File.Exists(Path.Combine(root, "docs", "issues", "mr-04.md")));
    }

    [Fact]
    public void Export_IgnoresMarkedFileOutsideTheKindFolder()
    {
        var root = CreateTempRoot();
        WriteMirrorFile(root, "docs/issues/spec.md", "<!-- forge: #5 -->\n\nSlice text\n");

        var files = DocsMirrorExporter.Export([Discussion(5, "[PRD] Spec", body: "PRD text")], root);

        Assert.Equal("docs/prd/spec.md", Assert.Single(files).Path);
        Assert.Equal("<!-- forge: #5 -->\n\nSlice text\n", ReadMirrorFile(root, "docs/issues/spec.md"));
    }

    [Fact]
    public void Export_SuffixesNumberWhenSlugPathBelongsToAnotherDiscussion()
    {
        var root = CreateTempRoot();
        WriteMirrorFile(root, "docs/prd/status-outage.md", "<!-- forge: #220 -->\n\nOld\n");

        var files = DocsMirrorExporter.Export(
            [
                Discussion(220, "[PRD] Status outage", body: "Canonical"),
                Discussion(213, "[PRD] Status outage", body: "Duplicate A"),
                Discussion(219, "[PRD] Status outage", body: "Duplicate B"),
            ],
            root);

        Assert.Equal(
            ["docs/prd/status-outage-213.md", "docs/prd/status-outage-219.md", "docs/prd/status-outage.md"],
            files.Select(file => file.Path));
        Assert.Equal("<!-- forge: #220 -->\n\nCanonical\n", ReadMirrorFile(root, "docs/prd/status-outage.md"));
    }

    [Fact]
    public void Export_GivesSharedSlugToLowestNumberInEmptyFolder()
    {
        var root = CreateTempRoot();

        var files = DocsMirrorExporter.Export(
            [
                Discussion(220, "[PRD] Status outage", body: "Newer"),
                Discussion(213, "[PRD] Status outage", body: "Older"),
            ],
            root);

        Assert.Equal(
            [(213, "docs/prd/status-outage.md"), (220, "docs/prd/status-outage-220.md")],
            files.Select(file => (file.Number, file.Path)));
    }

    [Fact]
    public void Export_DoesNotOverwriteUnmarkedFileAtSlugPath()
    {
        var root = CreateTempRoot();
        WriteMirrorFile(root, "docs/prd/spec.md", "# Hand-written notes\n");

        var files = DocsMirrorExporter.Export([Discussion(5, "[PRD] Spec", body: "Body")], root);

        Assert.Equal("docs/prd/spec-5.md", Assert.Single(files).Path);
        Assert.Equal("# Hand-written notes\n", ReadMirrorFile(root, "docs/prd/spec.md"));
    }

    private static void WriteMirrorFile(string root, string relativePath, string content)
    {
        var absolutePath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, content);
    }

    private static string ReadMirrorFile(string root, string relativePath) =>
        File.ReadAllText(Path.Combine(root, relativePath));

    private static DiscussionModel Discussion(
        int number,
        string title,
        string? body,
        params DiscussionCommentModel[] comments) =>
        new()
        {
            Number = number,
            Title = title,
            Body = body,
            CreatorUsername = "admin",
            CreatedAt = CreatedAt,
            Comments = comments,
        };

    private static DiscussionCommentModel Comment(
        string body,
        int secondsAfterCreate,
        string author = "admin",
        Guid? parentCommentId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            AuthorUsername = author,
            BodyMarkdown = body,
            CreatedAt = CreatedAt.AddSeconds(secondsAfterCreate),
            ParentCommentId = parentCommentId,
        };

    private string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ogb-docs-pull-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempPaths.Add(path);
        return path;
    }
}
