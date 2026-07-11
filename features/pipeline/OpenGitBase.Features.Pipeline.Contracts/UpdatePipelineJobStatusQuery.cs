using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class UpdatePipelineJobStatusQuery : IQuery<PipelineJobDto, UpdatePipelineJobStatusQuery>
{
    public PipelineJobId JobId { get; set; } = PipelineJobId.From(Guid.NewGuid());

    public PipelineJobStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;
}
