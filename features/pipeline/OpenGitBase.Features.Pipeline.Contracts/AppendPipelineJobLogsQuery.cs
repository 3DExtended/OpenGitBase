using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Pipeline.Contracts;

public sealed class AppendPipelineJobLogsQuery : IQuery<bool, AppendPipelineJobLogsQuery>
{
    public PipelineJobId JobId { get; set; } = PipelineJobId.From(Guid.NewGuid());

    public string LogSection { get; set; } = "script";

    public IReadOnlyList<string> LogLines { get; set; } = [];
}
