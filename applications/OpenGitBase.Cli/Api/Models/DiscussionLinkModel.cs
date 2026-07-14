namespace OpenGitBase.Cli.Api.Models;

public sealed class DiscussionLinkModel
{
    public int TargetDiscussionNumber { get; set; }

    public DiscussionRelationshipType RelationshipType { get; set; }

    public string? TargetDiscussionTitle { get; set; }

    public string? TargetDiscussionStatus { get; set; }
}
