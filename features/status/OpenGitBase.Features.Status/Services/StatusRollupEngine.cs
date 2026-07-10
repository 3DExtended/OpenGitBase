using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public static class StatusRollupEngine
{
    public static PublicHealthStatus RollupGroup(IEnumerable<PublicHealthStatus> statuses)
    {
        var list = statuses.ToList();
        if (list.Count == 0)
        {
            return PublicHealthStatus.Unhealthy;
        }

        if (list.Exists(status => status == PublicHealthStatus.Unhealthy))
        {
            return PublicHealthStatus.Unhealthy;
        }

        if (list.Exists(status => status == PublicHealthStatus.Degraded))
        {
            return PublicHealthStatus.Degraded;
        }

        return PublicHealthStatus.Healthy;
    }

    public static PublicHealthStatus RollupOverall(IEnumerable<PublicHealthStatus> groupStatuses) =>
        RollupGroup(groupStatuses);
}
