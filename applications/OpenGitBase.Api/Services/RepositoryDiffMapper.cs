#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
#pragma warning disable SA1412 // Store files as UTF-8 with byte order mark
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Models.StorageContent;

namespace OpenGitBase.Api.Services;

public static class RepositoryDiffMapper
{
    public static MergeRequestDiffFileResponse MapFile(StorageContentDiffFilePayload file)
    {
        var filePath = file.NewPath ?? file.OldPath ?? string.Empty;
        return new MergeRequestDiffFileResponse
        {
            FilePath = filePath,
            OldPath = file.OldPath,
            ChangeType = file.Status,
            Hunks = file.Hunks.Select(MapHunk).ToList(),
        };
    }

    public static MergeRequestDiffFileResponse MapCommitFile(StorageContentCommitFilePayload file)
    {
        if (!string.IsNullOrWhiteSpace(file.Path))
        {
            return new MergeRequestDiffFileResponse
            {
                FilePath = file.Path,
                ChangeType = file.ChangeType ?? "added",
                Hunks = [],
            };
        }

        var filePath = file.NewPath ?? file.OldPath ?? string.Empty;
        return new MergeRequestDiffFileResponse
        {
            FilePath = filePath,
            OldPath = file.OldPath,
            ChangeType = file.Status ?? string.Empty,
            Hunks = file.Hunks.Select(MapHunk).ToList(),
        };
    }

    private static MergeRequestDiffHunkResponse MapHunk(StorageContentDiffHunkPayload hunk) =>
        new()
        {
            Header =
                $"@@ -{hunk.OldStart},{hunk.OldLines} +{hunk.NewStart},{hunk.NewLines} @@",
            Lines = hunk.Lines.Select(MapLine).ToList(),
        };

    private static MergeRequestDiffLineResponse MapLine(StorageContentDiffLinePayload line) =>
        new()
        {
            OldLineNumber = line.OldLineNumber,
            NewLineNumber = line.NewLineNumber,
            Type = string.Equals(line.Type, "delete", StringComparison.OrdinalIgnoreCase)
                ? "remove"
                : line.Type,
            Content = line.Content,
        };
}
