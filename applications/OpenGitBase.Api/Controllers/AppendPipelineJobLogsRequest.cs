using OpenGitBase.Features.Pipeline.Contracts;

namespace OpenGitBase.Api.Controllers;

public sealed class AppendPipelineJobLogsRequest
{
    public string LogSection { get; set; } = "script";

    public IReadOnlyList<string>? LogLines { get; set; }
}
