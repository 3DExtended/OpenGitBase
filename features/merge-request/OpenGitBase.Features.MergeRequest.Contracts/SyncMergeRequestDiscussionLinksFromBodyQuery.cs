using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class SyncMergeRequestDiscussionLinksFromBodyQuery
    : IQuery<Unit, SyncMergeRequestDiscussionLinksFromBodyQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }

    public string? Body { get; set; }
}
