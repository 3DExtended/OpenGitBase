namespace OpenGitBase.Features.Discussion.Contracts;

public class CommentAnchorDto
{
    public string Ref { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int? EndLine { get; set; }
    public AnchorResolutionDto? Resolution { get; set; }
}
