﻿using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public static class ReplicaSetPlanner
{
    public const int RequiredHealthyNodes = 3;

    public static ReplicaSetSelection? SelectReplicaSet(IReadOnlyList<StorageNodeDto> healthyNodes)
    {
        if (healthyNodes.Count < RequiredHealthyNodes)
        {
            return null;
        }

        var ordered = healthyNodes
            .OrderByDescending(node => node.FreeBytesAvailable)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .Take(RequiredHealthyNodes)
            .ToList();

        return new ReplicaSetSelection(ordered[0], ordered[1], ordered[2]);
    }
}
