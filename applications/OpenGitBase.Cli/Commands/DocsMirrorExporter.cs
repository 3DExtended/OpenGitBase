using System.Text;
using System.Text.RegularExpressions;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static partial class DocsMirrorExporter
{
    private static readonly string[] DefaultPrefixes = ["PRD", "ADR", "slice"];

    private static readonly TimeSpan PublishBurstGap = TimeSpan.FromMinutes(10);

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public static IReadOnlyList<DiscussionModel> FilterByPrefix(
        IReadOnlyList<DiscussionModel> discussions,
        IReadOnlyList<string>? prefixFilters)
    {
        var prefixes = NormalizePrefixes(prefixFilters);
        return discussions
            .Where(discussion => prefixes.Any(prefix => MatchesPrefix(discussion.Title, prefix)))
            .ToList();
    }

    /// <summary>
    /// Writes one mirror file per discussion under <paramref name="outputRoot"/>. A discussion whose
    /// forge marker already sits in a file under its kind folder updates that file; otherwise it gets
    /// the slug path from its title, suffixed with its number when another file already holds that path.
    /// </summary>
    public static IReadOnlyList<DocsPullFileModel> Export(
        IReadOnlyList<DiscussionModel> discussions,
        string outputRoot)
    {
        var mirrorFiles = DocsMirrorMarker.ScanMirrorFiles(outputRoot);
        var takenPaths = mirrorFiles
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var exported = new List<DocsPullFileModel>();

        foreach (var discussion in discussions.OrderBy(discussion => discussion.Number))
        {
            var relativePath = ResolveExportPath(discussion, mirrorFiles, takenPaths);
            var absolutePath = Path.Combine(outputRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(absolutePath, BuildFileContent(discussion), Utf8WithoutBom);

            exported.Add(new DocsPullFileModel
            {
                Number = discussion.Number,
                Path = relativePath,
                Title = discussion.Title,
            });
        }

        return exported;
    }

    public static string BuildFileContent(DiscussionModel discussion)
    {
        var parts = CollectDocumentParts(discussion);
        var text = parts.Count == 0 ? discussion.Title.Trim() : string.Join("\n\n", parts);
        return $"{DocsMirrorMarker.Format(discussion.Number)}\n\n{text}\n";
    }

    public static string ResolveRelativePath(string title)
    {
        var (folder, fileName) = ResolveSlugLocation(title);
        return $"{folder}/{fileName}";
    }

    private static string ResolveExportPath(
        DiscussionModel discussion,
        IReadOnlyList<DocsMirrorFile> mirrorFiles,
        HashSet<string> takenPaths)
    {
        var (folder, fileName) = ResolveSlugLocation(discussion.Title);
        var markedFile = mirrorFiles.FirstOrDefault(file =>
            file.Number == discussion.Number
            && file.RelativePath.StartsWith($"{folder}/", StringComparison.OrdinalIgnoreCase));
        if (markedFile is not null)
        {
            return markedFile.RelativePath;
        }

        var path = $"{folder}/{fileName}";
        if (takenPaths.Contains(path))
        {
            path = $"{folder}/{Path.GetFileNameWithoutExtension(fileName)}-{discussion.Number}.md";
        }

        takenPaths.Add(path);
        return path;
    }

    private static (string Folder, string FileName) ResolveSlugLocation(string title)
    {
        if (TryMatchPrefix(title, "PRD", out var prdRemainder))
        {
            return ("docs/prd", $"{Slugify(prdRemainder)}.md");
        }

        if (TryMatchPrefix(title, "ADR", out var adrRemainder))
        {
            var match = AdrNumberRegex().Match(adrRemainder);
            var number = match.Success ? match.Groups[1].Value : "0000";
            var slug = match.Success ? adrRemainder[(match.Index + match.Length)..] : adrRemainder;
            return ("docs/adr", $"{number}-{Slugify(slug)}.md");
        }

        if (TryMatchPrefix(title, "slice", out var sliceRemainder))
        {
            return ("docs/issues", $"{ExtractSliceId(sliceRemainder)}.md");
        }

        throw new InvalidOperationException($"Unsupported discussion title for export: {title}");
    }

    /// <summary>
    /// The document is the discussion body followed by the creator's top-level comments posted in the
    /// same publish burst (each within <see cref="PublishBurstGap"/> of the previous post). The API
    /// echoes the body as the opening comment, and only comments are editable, so that echo replaces the
    /// body when it matches or has been edited. Later comments (audits, status notes) and comments by
    /// anyone else stay forge-only.
    /// </summary>
    private static List<string> CollectDocumentParts(DiscussionModel discussion)
    {
        var parts = new List<string>();
        var body = discussion.Body?.Trim() ?? string.Empty;
        if (body.Length > 0)
        {
            parts.Add(body);
        }

        var rootComments = (discussion.Comments ?? [])
            .Where(comment => comment.ParentCommentId is null)
            .OrderBy(comment => comment.CreatedAt);

        var previousPostAt = discussion.CreatedAt;
        var isOpeningComment = true;
        foreach (var comment in rootComments)
        {
            if (!string.Equals(comment.AuthorUsername, discussion.CreatorUsername, StringComparison.Ordinal)
                || comment.CreatedAt - previousPostAt > PublishBurstGap)
            {
                break;
            }

            previousPostAt = comment.CreatedAt;
            var text = comment.BodyMarkdown.Trim();
            var isBodyEcho = isOpeningComment
                && body.Length > 0
                && (comment.EditedAt is not null || string.Equals(text, body, StringComparison.Ordinal));
            isOpeningComment = false;
            if (isBodyEcho)
            {
                parts[0] = text;
            }
            else if (text.Length > 0)
            {
                parts.Add(text);
            }
        }

        return parts;
    }

    private static IReadOnlyList<string> NormalizePrefixes(IReadOnlyList<string>? prefixFilters)
    {
        if (prefixFilters is null || prefixFilters.Count == 0)
        {
            return DefaultPrefixes;
        }

        return prefixFilters
            .Select(prefix => prefix.Trim().Trim('[', ']'))
            .Where(prefix => prefix.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesPrefix(string title, string prefix) =>
        TryMatchPrefix(title, prefix, out _);

    private static bool TryMatchPrefix(string title, string prefix, out string remainder)
    {
        remainder = string.Empty;
        if (!title.StartsWith('['))
        {
            return false;
        }

        var closing = title.IndexOf(']');
        if (closing <= 1)
        {
            return false;
        }

        var tag = title[1..closing];
        if (!tag.Equals(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        remainder = title[(closing + 1)..].TrimStart();
        return true;
    }

    private static string ExtractSliceId(string remainder)
    {
        var head = remainder.Split('—', 2, StringSplitOptions.TrimEntries)[0];
        if (string.IsNullOrWhiteSpace(head))
        {
            throw new InvalidOperationException("Slice title must include an id after [slice].");
        }

        return head.Trim();
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = NonAlphanumericRegex().Replace(normalized, "-");
        normalized = HyphenCollapseRegex().Replace(normalized, "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "untitled" : normalized;
    }

    [GeneratedRegex(@"^(\d{4})\b")]
    private static partial Regex AdrNumberRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex HyphenCollapseRegex();
}
