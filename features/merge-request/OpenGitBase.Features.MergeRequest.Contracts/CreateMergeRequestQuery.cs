using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class CreateMergeRequestQuery : IQuery<MergeRequestDto, CreateMergeRequestQuery>
{
    public Guid RepositoryId { get; set; }
    public UserId CreatorUserId { get; set; } = UserId.From(Guid.Empty);
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string SourceRef { get; set; } = string.Empty;
    public string TargetRef { get; set; } = string.Empty;
    public string SourceHeadSha { get; set; } = string.Empty;
    public string TargetBaseSha { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
}
