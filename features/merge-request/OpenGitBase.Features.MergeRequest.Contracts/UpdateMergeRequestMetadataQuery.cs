using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class UpdateMergeRequestMetadataQuery : IQuery<MergeRequestDto, UpdateMergeRequestMetadataQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool ClearBody { get; set; }
}
