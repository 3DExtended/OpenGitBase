namespace OpenGitBase.Api.Models;

public sealed class CreateMergeRequestRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string SourceRef { get; set; } = string.Empty;
    public string TargetRef { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
}
