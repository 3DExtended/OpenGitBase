using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class CreateDiscussionLinkQuery
    : IQuery<DiscussionLinkDto, CreateDiscussionLinkQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }

    public int TargetDiscussionNumber { get; set; }

    public DiscussionRelationshipType RelationshipType { get; set; }
}
