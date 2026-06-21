using OpenGitBase.Api.Models;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class WebReadReplicaSelector
{
    public WebReadReplicaSelection? Select(RepositoryReplicationRoutingDto routing)
    {
        var healthyTargets = routing.Targets.Where(target => target.IsHealthy).ToList();
        var nonPrimary = healthyTargets.Where(target => !target.IsPrimary).ToList();
        if (nonPrimary.Count > 0)
        {
            var selected = nonPrimary[0];
            return new WebReadReplicaSelection
            {
                Target = selected,
                ReplicationLag = BuildLag(selected),
            };
        }

        if (healthyTargets.Count == 1 && healthyTargets[0].IsPrimary)
        {
            var onlyPrimary = healthyTargets[0];
            return new WebReadReplicaSelection
            {
                Target = onlyPrimary,
                ReplicationLag = new RepositoryReplicationLagDto { Behind = false },
            };
        }

        return null;
    }

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
