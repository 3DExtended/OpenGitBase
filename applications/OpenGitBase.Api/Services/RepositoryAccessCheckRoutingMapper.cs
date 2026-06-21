using OpenGitBase.Api.Models;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public static class RepositoryAccessCheckRoutingMapper
{
    public static StorageRoutingTarget MapRoutingTarget(RepositoryRoutingTargetDto target) =>
        new()
        {
            InternalHost = target.InternalHost,
            InternalSshPort = target.InternalSshPort,
            InternalGitHttpPort = target.InternalGitHttpPort,
            Role = target.Role,
        };
}
