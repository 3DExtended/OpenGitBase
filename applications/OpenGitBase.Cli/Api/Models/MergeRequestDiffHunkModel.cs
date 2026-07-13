namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestDiffHunkModel
{
    public string Header { get; set; } = string.Empty;

    public IReadOnlyList<MergeRequestDiffLineModel> Lines { get; set; } = [];
}
