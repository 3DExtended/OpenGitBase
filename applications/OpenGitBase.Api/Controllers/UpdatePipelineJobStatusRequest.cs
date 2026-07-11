using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Api.Controllers;

public sealed class UpdatePipelineJobStatusRequest
{
    public PipelineJobStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? LogSection { get; set; }

    public IReadOnlyList<string>? LogLines { get; set; }
}
