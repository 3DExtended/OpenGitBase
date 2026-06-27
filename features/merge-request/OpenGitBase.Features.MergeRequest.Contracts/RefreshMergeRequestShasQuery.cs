using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class RefreshMergeRequestShasQuery : IQuery<MergeRequestDto, RefreshMergeRequestShasQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public string SourceHeadSha { get; set; } = string.Empty;
    public string TargetBaseSha { get; set; } = string.Empty;
}
