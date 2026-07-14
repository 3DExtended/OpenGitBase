using System.Text.RegularExpressions;
using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static partial class DocsMirrorFreshnessChecker
{
    private static readonly string[] MirrorRoots = ["docs/prd", "docs/adr", "docs/issues"];

    public static IReadOnlyList<string> FindMirrorFilesMissingForgeMarker(string repoRoot)
    {
        var missing = new List<string>();
        foreach (var relativeRoot in MirrorRoots)
        {
            var absoluteRoot = Path.Combine(repoRoot, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*.md", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                if (!ForgeMarkerRegex().IsMatch(content))
                {
                    missing.Add(Path.GetRelativePath(repoRoot, file).Replace('\\', '/'));
                }
            }
        }

        return missing;
    }

    public static IReadOnlyList<string> FindStaleMirrorPaths(
        IReadOnlyList<DocsPullFileModel> pulled,
        string repoRoot)
    {
        var expected = pulled
            .Select(file => file.Path.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = new List<string>();

        foreach (var relativeRoot in MirrorRoots)
        {
            var absoluteRoot = Path.Combine(repoRoot, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*.md", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                if (ForgeMarkerRegex().IsMatch(File.ReadAllText(file)) && !expected.Contains(relativePath))
                {
                    stale.Add(relativePath);
                }
            }
        }

        return stale;
    }

    [GeneratedRegex(@"<!--\s*forge:\s*#\d+\s*-->")]
    private static partial Regex ForgeMarkerRegex();
}
