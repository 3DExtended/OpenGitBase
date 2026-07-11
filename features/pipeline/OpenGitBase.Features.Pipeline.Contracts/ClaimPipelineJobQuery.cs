using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class ClaimPipelineJobQuery : IQuery<ClaimPipelineJobResultDto, ClaimPipelineJobQuery>
{
    public Guid ComputeNodeId { get; set; }

    public IReadOnlyList<string> HostingProfiles { get; set; } = Array.Empty<string>();
}
