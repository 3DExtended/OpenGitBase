using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public static class StorageNodeSelection
{
    public static StorageNodeDto? SelectBestNode(IReadOnlyList<StorageNodeDto> healthyNodes)
    {
        if (healthyNodes.Count == 0)
        {
            return null;
        }

        return healthyNodes
            .OrderByDescending(node => node.FreeBytesAvailable)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .First();
    }
}
