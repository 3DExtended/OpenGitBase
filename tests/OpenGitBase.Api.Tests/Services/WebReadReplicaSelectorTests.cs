using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class WebReadReplicaSelectorTests
{
    private readonly WebReadReplicaSelector _selector = new();

    [Fact]
    public void Select_WhenHealthyNonPrimaryExists_NeverPicksPrimary()
    {
        var primaryId = Guid.NewGuid();
        var replicaId = Guid.NewGuid();
        var routing = new RepositoryReplicationRoutingDto
        {
            WriteQuorumAvailable = true,
            Targets =
            [
                CreateTarget(primaryId, isPrimary: true, isHealthy: true),
                CreateTarget(replicaId, isPrimary: false, isHealthy: true),
            ],
        };

        var selection = _selector.Select(routing);

        Assert.NotNull(selection);
        Assert.Equal(replicaId, selection.Target.StorageNodeId);
        Assert.False(selection.Target.IsPrimary);
    }

    [Fact]
    public void Select_WhenOnlyHealthyPrimaryExists_ReturnsPrimary()
    {
        var primaryId = Guid.NewGuid();
        var routing = new RepositoryReplicationRoutingDto
        {
            WriteQuorumAvailable = true,
            Targets =
            [
                CreateTarget(primaryId, isPrimary: true, isHealthy: true),
                CreateTarget(Guid.NewGuid(), isPrimary: false, isHealthy: false),
            ],
        };

        var selection = _selector.Select(routing);

        Assert.NotNull(selection);
        Assert.Equal(primaryId, selection.Target.StorageNodeId);
        Assert.True(selection.Target.IsPrimary);
    }

    [Fact]
    public void Select_WhenNoHealthyTargets_ReturnsNull()
    {
        var routing = new RepositoryReplicationRoutingDto
        {
            WriteQuorumAvailable = false,
            Targets =
            [
                CreateTarget(Guid.NewGuid(), isPrimary: true, isHealthy: false),
                CreateTarget(Guid.NewGuid(), isPrimary: false, isHealthy: false),
            ],
        };

        var selection = _selector.Select(routing);

        Assert.Null(selection);
    }

    [Fact]
    public void Select_WhenNonPrimaryOutOfSync_ReportsReplicationLag()
    {
        var replicaId = Guid.NewGuid();
        var routing = new RepositoryReplicationRoutingDto
        {
            WriteQuorumAvailable = true,
            Targets =
            [
                CreateTarget(Guid.NewGuid(), isPrimary: true, isHealthy: true),
                CreateTarget(replicaId, isPrimary: false, isHealthy: true, isInSync: false),
            ],
        };

        var selection = _selector.Select(routing);

        Assert.NotNull(selection);
        Assert.True(selection.ReplicationLag?.Behind);
    }

    private static RepositoryRoutingTargetDto CreateTarget(
        Guid storageNodeId,
        bool isPrimary,
        bool isHealthy,
        bool isInSync = true
    ) =>
        new()
        {
            StorageNodeId = storageNodeId,
            InternalHost = "storage.local",
            InternalHttpPort = 8080,
            IsPrimary = isPrimary,
            IsHealthy = isHealthy,
            IsInSync = isInSync,
        };
}
