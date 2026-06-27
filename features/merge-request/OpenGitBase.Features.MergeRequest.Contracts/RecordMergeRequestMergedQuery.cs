using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class RecordMergeRequestMergedQuery : IQuery<MergeRequestDto, RecordMergeRequestMergedQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }

    public string MergeCommitSha { get; set; } = string.Empty;
}
