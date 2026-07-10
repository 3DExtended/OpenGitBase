using OpenGitBase.Common.Options;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Status.Services;

public sealed class StorageGroupStatusBuilder
{
    private readonly StorageNodeOptions _options;

    public StorageGroupStatusBuilder(StorageNodeOptions options)
    {
        _options = options;
    }

    public StatusGroupSnapshot Build(
        IReadOnlyList<StorageNodeDto> nodes,
        DateTimeOffset checkedAt
    )
    {
        var instances = nodes
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .Select(node => MapStorageNode(node, checkedAt))
            .ToList();

        var healthyCount = instances.Count(instance => instance.Status == PublicHealthStatus.Healthy);
        var groupStatus = healthyCount switch
        {
            >= 3 => PublicHealthStatus.Healthy,
            2 => PublicHealthStatus.Degraded,
            _ => PublicHealthStatus.Unhealthy,
        };

        return new StatusGroupSnapshot
        {
            Group = StatusComponentGroup.Storage,
            Status = groupStatus,
            Instances = instances,
        };
    }

    private StatusInstanceSnapshot MapStorageNode(StorageNodeDto node, DateTimeOffset checkedAt)
    {
        if (!node.IsHealthy)
        {
            return new StatusInstanceSnapshot
            {
                InstanceId = node.NodeId,
                Status = PublicHealthStatus.Unhealthy,
                LastCheckedAt = checkedAt,
                LastSeenAt = node.LastHeartbeatAt,
                Message = "Node unhealthy",
            };
        }

        var cutoff = checkedAt.AddSeconds(-_options.MissedHeartbeatThresholdSeconds);
        if (node.LastHeartbeatAt is null || node.LastHeartbeatAt < cutoff)
        {
            return new StatusInstanceSnapshot
            {
                InstanceId = node.NodeId,
                Status = PublicHealthStatus.Degraded,
                LastCheckedAt = checkedAt,
                LastSeenAt = node.LastHeartbeatAt,
                Message = "Heartbeat stale",
            };
        }

        return new StatusInstanceSnapshot
        {
            InstanceId = node.NodeId,
            Status = PublicHealthStatus.Healthy,
            LastCheckedAt = checkedAt,
            LastSeenAt = node.LastHeartbeatAt,
        };
    }
}
