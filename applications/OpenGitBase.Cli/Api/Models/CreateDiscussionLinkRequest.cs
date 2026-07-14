namespace OpenGitBase.Cli.Api.Models;

public sealed class CreateDiscussionLinkRequest
{
    public int TargetDiscussionNumber { get; set; }

    public string RelationshipType { get; set; } = "related";
}
