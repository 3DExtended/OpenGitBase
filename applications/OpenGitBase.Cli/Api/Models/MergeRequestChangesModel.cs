namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestChangesModel
{
    public IReadOnlyList<MergeRequestDiffFileModel> Files { get; set; } = [];
}
