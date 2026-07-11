namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class PipelineJobDto
{
    public PipelineJobId Id { get; set; } = PipelineJobId.From(Guid.NewGuid());

    public PipelineRunId RunId { get; set; } = PipelineRunId.From(Guid.NewGuid());

    public string Name { get; set; } = string.Empty;

    public string Stage { get; set; } = string.Empty;

    public string RunsOn { get; set; } = string.Empty;

    public PipelineJobStatus Status { get; set; } = PipelineJobStatus.Queued;

    public string Script { get; set; } = string.Empty;

    public string ResolvedSpecJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
