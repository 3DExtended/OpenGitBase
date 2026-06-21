using OpenGitBase.Dispatcher.Models;

namespace OpenGitBase.Dispatcher.Services;

public static class GitRoutingTargetSelector
{
    public static StorageRoutingTarget SelectWriteTarget(RepositoryAccessCheckResponse accessCheck)
    {
        if (accessCheck.Primary is not null)
        {
            return accessCheck.Primary;
        }

        if (
            string.IsNullOrWhiteSpace(accessCheck.StorageNodeInternalHost)
            || accessCheck.StorageNodeInternalSshPort is null
            || accessCheck.StorageNodeInternalGitHttpPort is null
        )
        {
            throw new InvalidOperationException("Access check is missing write routing fields.");
        }

        return new StorageRoutingTarget
        {
            InternalHost = accessCheck.StorageNodeInternalHost,
            InternalSshPort = accessCheck.StorageNodeInternalSshPort.Value,
            InternalGitHttpPort = accessCheck.StorageNodeInternalGitHttpPort.Value,
            Role = "Primary",
        };
    }

    public static StorageRoutingTarget SelectReadTarget(RepositoryAccessCheckResponse accessCheck)
    {
        if (accessCheck.ReadTargets is { Count: > 0 })
        {
            return accessCheck.ReadTargets[0];
        }

        return SelectWriteTarget(accessCheck);
    }
}
