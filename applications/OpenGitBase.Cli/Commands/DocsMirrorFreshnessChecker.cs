using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli.Commands;

public static class DocsMirrorFreshnessChecker
{
    public static IReadOnlyList<string> FindMirrorFilesMissingForgeMarker(string repoRoot) =>
        DocsMirrorMarker.ScanMirrorFiles(repoRoot)
            .Where(file => file.Number is null)
            .Select(file => file.RelativePath)
            .ToList();

    /// <summary>
    /// Marked mirror files that a pull into <paramref name="repoRoot"/> did not write: their discussion
    /// is gone from the forge, or another file already carries the same marker.
    /// </summary>
    public static IReadOnlyList<string> FindStaleMirrorPaths(
        IReadOnlyList<DocsPullFileModel> pulled,
        string repoRoot)
    {
        var expected = pulled
            .Select(file => file.Path.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return DocsMirrorMarker.ScanMirrorFiles(repoRoot)
            .Where(file => file.Number is not null && !expected.Contains(file.RelativePath))
            .Select(file => file.RelativePath)
            .ToList();
    }
}
