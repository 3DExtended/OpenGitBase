namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class PipelineJobLogDto
{
    public string Section { get; set; } = "script";

    public string Line { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
