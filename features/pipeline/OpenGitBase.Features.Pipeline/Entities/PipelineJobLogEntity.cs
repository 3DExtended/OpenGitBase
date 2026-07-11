using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Pipeline.Entities;

public sealed class PipelineJobLogEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public string Section { get; set; } = "script";

    public string Line { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }
}
