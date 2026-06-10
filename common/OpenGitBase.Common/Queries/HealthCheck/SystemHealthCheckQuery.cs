using OpenGitBase.Common.Models.HealthCheck;
using OpenGitBase.Cqrs;

namespace OpenGitBase.Common.Queries.HealthCheck;

public class SystemHealthCheckQuery : IQuery<HealthCheckReport, SystemHealthCheckQuery>
{
    public bool IncludeDetails { get; set; } = true;

    public int TimeoutMs { get; set; } = 5000;

    public bool RunInParallel { get; set; } = true;
}
