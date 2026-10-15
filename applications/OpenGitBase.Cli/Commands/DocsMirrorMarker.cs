using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenGitBase.Cli.Commands;

/// <summary>
/// The <c>&lt;!-- forge: #N --&gt;</c> marker that ties a git mirror file to its forge discussion.
/// </summary>
public static partial class DocsMirrorMarker
{
    private static readonly string[] MirrorRoots = ["docs/prd", "docs/adr", "docs/issues"];

    public static string Format(int number) => $"<!-- forge: #{number} -->";

    /// <summary>
    /// Lists every markdown file under the mirror roots, ordered by path, with the discussion number
    /// from its first forge marker (null when the file has none).
    /// </summary>
    public static IReadOnlyList<DocsMirrorFile> ScanMirrorFiles(string repoRoot)
    {
        var files = new List<DocsMirrorFile>();
        foreach (var relativeRoot in MirrorRoots)
        {
            var absoluteRoot = Path.Combine(repoRoot, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(absoluteRoot, "*.md", SearchOption.AllDirectories))
            {
                files.Add(new DocsMirrorFile(
                    Path.GetRelativePath(repoRoot, file).Replace('\\', '/'),
                    ReadNumber(File.ReadAllText(file))));
            }
        }

        return files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToList();
    }

    private static int? ReadNumber(string content)
    {
        var match = ForgeMarkerRegex().Match(content);
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    [GeneratedRegex(@"<!--\s*forge:\s*#(\d+)\s*-->")]
    private static partial Regex ForgeMarkerRegex();
}
