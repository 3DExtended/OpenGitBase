namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class ClaimPipelineJobResultDto
{
    public PipelineJobDto Job { get; set; } = new();

    public string JobIdentityToken { get; set; } = string.Empty;
}
