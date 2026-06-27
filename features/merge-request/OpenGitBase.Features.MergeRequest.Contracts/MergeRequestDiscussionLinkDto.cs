namespace OpenGitBase.Features.MergeRequest.Contracts;

public class MergeRequestDiscussionLinkDto
{
    public int DiscussionNumber { get; set; }

    public MergeRequestRelationshipType RelationshipType { get; set; }

    public string? DiscussionTitle { get; set; }

    public string? DiscussionStatus { get; set; }
}
