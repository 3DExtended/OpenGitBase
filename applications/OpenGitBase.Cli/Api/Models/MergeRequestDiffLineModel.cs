namespace OpenGitBase.Cli.Api.Models;

public sealed class MergeRequestDiffLineModel
{
    public int? OldLineNumber { get; set; }

    public int? NewLineNumber { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
