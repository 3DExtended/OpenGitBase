using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class CreateMergeRequestDiscussionLinkQuery
    : IQuery<MergeRequestDiscussionLinkDto, CreateMergeRequestDiscussionLinkQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }

    public int DiscussionNumber { get; set; }

    public MergeRequestRelationshipType RelationshipType { get; set; }
}
