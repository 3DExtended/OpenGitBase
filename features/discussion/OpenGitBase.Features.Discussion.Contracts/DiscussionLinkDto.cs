namespace OpenGitBase.Features.Discussion.Contracts;

public class DiscussionLinkDto
{
    public int TargetDiscussionNumber { get; set; }

    public DiscussionRelationshipType RelationshipType { get; set; }

    public string? TargetDiscussionTitle { get; set; }

    public string? TargetDiscussionStatus { get; set; }
}
