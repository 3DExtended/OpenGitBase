using System.Text;
using System.Text.RegularExpressions;

namespace OpenGitBase.E2E.Core;

public sealed class PromotionCandidate
{
    public string SourceFile { get; init; } = string.Empty;

    public string TestMethod { get; init; } = string.Empty;

    public string SuggestedFeature { get; init; } = string.Empty;

    public string SuggestedCatalogStatus { get; init; } = "pending";

    public string MatchReason { get; init; } = string.Empty;
}

public static class PromotionIndexer
{
    private static readonly (Regex Pattern, string Feature)[] FeatureRules =
    [
        (new Regex("Organization|Org", RegexOptions.IgnoreCase), "F02 Organizations"),
        (new Regex("RepositoryMember|Member", RegexOptions.IgnoreCase), "F04 Members"),
        (new Regex("Discussion", RegexOptions.IgnoreCase), "F06 Discussion"),
        (new Regex("MergeRequest|Merge", RegexOptions.IgnoreCase), "F07 Merge requests"),
        (new Regex("Auth|Account|SignIn|Register", RegexOptions.IgnoreCase), "F01 Auth"),
        (new Regex("Git|Pat|Https", RegexOptions.IgnoreCase), "F08 Git HTTPS"),
        (new Regex("Browse|Content|Blob", RegexOptions.IgnoreCase), "F05 Browse"),
        (new Regex("Admin|Fleet", RegexOptions.IgnoreCase), "F11 Admin"),
        (new Regex("Notification|Discover", RegexOptions.IgnoreCase), "F12 Discovery"),
    ];

    private static readonly string[] PromotionMarkers =
    [
        "Unauthorized",
        "Forbidden",
        "NotFound",
        "HappyPath",
        "Returns401",
        "Returns403",
        "Returns404",
        "AccessDenied",
    ];

    public static IReadOnlyList<PromotionCandidate> Scan(string repoRoot)
    {
        var candidates = new List<PromotionCandidate>();
        var testRoots = new[]
        {
            Path.Combine(repoRoot, "tests"),
        };

        foreach (var root in testRoots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("OpenGitBase.E2E", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!file.Contains(".Tests", StringComparison.Ordinal))
                {
                    continue;
                }

                ScanFile(file, repoRoot, candidates);
            }
        }

        return candidates
            .GroupBy(c => $"{c.SourceFile}:{c.TestMethod}")
            .Select(g => g.First())
            .OrderBy(c => c.SuggestedFeature)
            .ThenBy(c => c.SourceFile)
            .ToList();
    }

    public static Task WriteMarkdownAsync(
        IReadOnlyList<PromotionCandidate> candidates,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# E2E promotion candidates");
        sb.AppendLine();
        sb.AppendLine($"Generated {DateTimeOffset.UtcNow:u} — {candidates.Count} candidates.");
        sb.AppendLine();
        sb.AppendLine("| Source | Method | Feature | Reason | Status |");
        sb.AppendLine("|--------|--------|---------|--------|--------|");
        foreach (var candidate in candidates)
        {
            sb.AppendLine(
                $"| `{candidate.SourceFile}` | `{candidate.TestMethod}` | {candidate.SuggestedFeature} | {candidate.MatchReason} | {candidate.SuggestedCatalogStatus} |");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        return File.WriteAllTextAsync(outputPath, sb.ToString(), cancellationToken);
    }

    private static void ScanFile(string file, string repoRoot, List<PromotionCandidate> candidates)
    {
        var text = File.ReadAllText(file);
        if (!PromotionMarkers.Any(m => text.Contains(m, StringComparison.Ordinal)))
        {
            return;
        }

        var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        var feature = InferFeature(relative, text);
        foreach (Match match in Regex.Matches(text, @"\b(?:public\s+)?(?:async\s+)?Task\s+(\w+)\s*\("))
        {
            var method = match.Groups[1].Value;
            if (method is "InitializeAsync" or "DisposeAsync")
            {
                continue;
            }

            var methodBodyStart = match.Index;
            var snippet = text.Substring(methodBodyStart, Math.Min(400, text.Length - methodBodyStart));
            var reason = PromotionMarkers.FirstOrDefault(m => snippet.Contains(m, StringComparison.Ordinal)) ?? "cross-boundary";
            candidates.Add(new PromotionCandidate
            {
                SourceFile = relative,
                TestMethod = method,
                SuggestedFeature = feature,
                MatchReason = reason,
            });
        }
    }

    private static string InferFeature(string relativePath, string text)
    {
        foreach (var (pattern, feature) in FeatureRules)
        {
            if (pattern.IsMatch(relativePath) || pattern.IsMatch(text))
            {
                return feature;
            }
        }

        return "F03 Repository settings";
    }
}
