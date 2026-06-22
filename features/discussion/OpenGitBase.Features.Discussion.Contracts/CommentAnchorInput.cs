namespace OpenGitBase.Features.Discussion.Contracts;

public class CommentAnchorInput
{
    public string Ref { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int? EndLine { get; set; }
}
