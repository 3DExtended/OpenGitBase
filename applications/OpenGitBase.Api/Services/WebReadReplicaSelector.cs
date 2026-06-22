using OpenGitBase.Api.Models;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class WebReadReplicaSelector
{
    public WebReadReplicaSelection? Select(RepositoryReplicationRoutingDto routing)
    {
        var healthyTargets = routing.Targets.Where(target => target.IsHealthy).ToList();
        var healthyNonPrimary = healthyTargets.Where(target => !target.IsPrimary).ToList();

        var inSyncNonPrimary = healthyNonPrimary.Where(target => target.IsInSync).ToList();
        if (inSyncNonPrimary.Count > 0)
        {
            return SelectTarget(inSyncNonPrimary[0]);
        }

        if (healthyNonPrimary.Count > 0)
        {
            return SelectTarget(healthyNonPrimary[0]);
        }

        var primary = healthyTargets.FirstOrDefault(target => target.IsPrimary);
        if (primary is not null)
        {
            return SelectTarget(primary);
        }

        return null;
    }

    private static WebReadReplicaSelection SelectTarget(RepositoryRoutingTargetDto target) =>
        new()
        {
            Target = target,
            ReplicationLag = BuildLag(target),
        };

    private static RepositoryReplicationLagDto? BuildLag(RepositoryRoutingTargetDto target)
    {
        if (target.IsInSync)
        {
            return new RepositoryReplicationLagDto { Behind = false };
        }

        return new RepositoryReplicationLagDto
        {
            Behind = true,
            Message = "Replica is syncing with primary.",
        };
    }
}
