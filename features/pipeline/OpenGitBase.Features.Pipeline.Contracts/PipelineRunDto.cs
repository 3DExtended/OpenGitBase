namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class PipelineRunDto
{
    public PipelineRunId Id { get; set; } = PipelineRunId.From(Guid.NewGuid());

    public Guid RepositoryId { get; set; }

    public string Ref { get; set; } = string.Empty;

    public string AfterSha { get; set; } = string.Empty;

    public PipelineRunStatus Status { get; set; } = PipelineRunStatus.Queued;

    public DateTimeOffset CreatedAt { get; set; }

    public IReadOnlyList<PipelineJobDto> Jobs { get; set; } = [];
}
