namespace OpenGitBase.Features.Discussion.Contracts;

public class AnchorResolutionDto
{
    public string Kind { get; set; } = "located";
    public string? FilePath { get; set; }
    public int? Line { get; set; }
}
