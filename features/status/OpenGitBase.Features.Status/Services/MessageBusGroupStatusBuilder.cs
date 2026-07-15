using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public sealed class MessageBusGroupStatusBuilder
{
    public StatusGroupSnapshot Build(IReadOnlyList<StatusInstanceSnapshot> instances)
    {
        var groupStatus = Rollup(instances);

        return new StatusGroupSnapshot
        {
            Group = StatusComponentGroup.MessageBus,
            Status = groupStatus,
            Instances = instances.ToList(),
        };
    }

    private static PublicHealthStatus Rollup(IReadOnlyList<StatusInstanceSnapshot> instances)
    {
        if (instances.Count == 0)
        {
            return PublicHealthStatus.Unhealthy;
        }

        if (instances.Count >= 3)
        {
            var healthyCount = instances.Count(
                instance => instance.Status == PublicHealthStatus.Healthy
            );
            return healthyCount switch
            {
                >= 3 => PublicHealthStatus.Healthy,
                2 => PublicHealthStatus.Degraded,
                _ => PublicHealthStatus.Unhealthy,
            };
        }

        return StatusRollupEngine.RollupGroup(instances.Select(instance => instance.Status));
    }
}
