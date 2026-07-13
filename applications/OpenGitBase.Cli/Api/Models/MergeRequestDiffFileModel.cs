namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestDiffFileModel
{
    public string FilePath { get; set; } = string.Empty;

    public string? OldPath { get; set; }

    public string ChangeType { get; set; } = string.Empty;

    public IReadOnlyList<MergeRequestDiffHunkModel> Hunks { get; set; } = [];
}
