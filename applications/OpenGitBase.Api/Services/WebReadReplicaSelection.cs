using OpenGitBase.Api.Models;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class WebReadReplicaSelection
{
    public required RepositoryRoutingTargetDto Target { get; init; }

    public RepositoryReplicationLagDto? ReplicationLag { get; init; }
}
