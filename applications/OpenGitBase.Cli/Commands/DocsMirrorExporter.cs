using System.Text;
using System.Text.RegularExpressions;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static partial class DocsMirrorExporter
{
    private static readonly string[] DefaultPrefixes = ["PRD", "ADR", "slice"];

    public static IReadOnlyList<DiscussionModel> FilterByPrefix(
        IReadOnlyList<DiscussionModel> discussions,
        IReadOnlyList<string>? prefixFilters)
    {
        var prefixes = NormalizePrefixes(prefixFilters);
        return discussions
            .Where(discussion => prefixes.Any(prefix => MatchesPrefix(discussion.Title, prefix)))
            .ToList();
    }

    public static DocsPullFileModel BuildExportFile(DiscussionModel discussion, string outputRoot)
    {
        var relativePath = ResolveRelativePath(discussion.Title);
        var absolutePath = Path.Combine(outputRoot, relativePath);
        var content = BuildFileContent(discussion);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        File.WriteAllText(absolutePath, content, Encoding.UTF8);

        return new DocsPullFileModel
        {
            Number = discussion.Number,
            Path = relativePath.Replace('\\', '/'),
            Title = discussion.Title,
        };
    }

    public static string BuildFileContent(DiscussionModel discussion)
    {
        var body = string.IsNullOrWhiteSpace(discussion.Body) ? discussion.Title : discussion.Body!;
        return $"<!-- forge: #{discussion.Number} -->\n\n{body.Trim()}\n";
    }

    public static string ResolveRelativePath(string title)
    {
        if (TryMatchPrefix(title, "PRD", out var prdRemainder))
        {
            return Path.Combine("docs", "prd", $"{Slugify(prdRemainder)}.md");
        }

        if (TryMatchPrefix(title, "ADR", out var adrRemainder))
        {
            var match = AdrNumberRegex().Match(adrRemainder);
            var number = match.Success ? match.Groups[1].Value : "0000";
            var slug = match.Success ? adrRemainder[(match.Index + match.Length)..] : adrRemainder;
            return Path.Combine("docs", "adr", $"{number}-{Slugify(slug)}.md");
        }

        if (TryMatchPrefix(title, "slice", out var sliceRemainder))
        {
            var sliceId = ExtractSliceId(sliceRemainder);
            return Path.Combine("docs", "issues", $"{sliceId}.md");
        }

        throw new InvalidOperationException($"Unsupported discussion title for export: {title}");
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
