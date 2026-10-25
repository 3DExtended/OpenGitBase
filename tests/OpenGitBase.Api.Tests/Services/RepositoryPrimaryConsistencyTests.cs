using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class RepositoryPrimaryConsistencyTests
{
    private static readonly Guid Storage1 = Guid.NewGuid();
    private static readonly Guid Storage3 = Guid.NewGuid();
    private static readonly Guid Storage4 = Guid.NewGuid();

    [Fact]
    public void FindProblems_WhenPointerAndPrimaryRowNameOneNode_FindsNone()
    {
        var repository = Repository(
            primaryPointer: Storage3,
            (Storage3, RepositoryReplicaRole.Primary),
            (Storage1, RepositoryReplicaRole.EncryptedReplica),
            (Storage4, RepositoryReplicaRole.Replica)
        );

        Assert.Empty(RepositoryPrimaryConsistency.FindProblems(repository));
    }

    [Fact]
    public void FindProblems_WhenThePointerNamesAnEncryptedReplica_ReportsIt()
    {
        // Production, "Notes": the backfill made storage-1 Primary and storage-3 EncryptedReplica but left the pointer.
        var repository = Repository(
            primaryPointer: Storage3,
            (Storage3, RepositoryReplicaRole.EncryptedReplica),
            (Storage1, RepositoryReplicaRole.Primary),
            (Storage4, RepositoryReplicaRole.Replica)
        );

        var problem = Assert.Single(RepositoryPrimaryConsistency.FindProblems(repository));
        Assert.Contains("EncryptedReplica", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_WhenNoRowIsOnThePointedNode_ReportsIt()
    {
        var repository = Repository(
            primaryPointer: Storage3,
            (Storage1, RepositoryReplicaRole.Primary),
            (Storage4, RepositoryReplicaRole.Replica)
        );

        Assert.Single(RepositoryPrimaryConsistency.FindProblems(repository));
    }

    [Fact]
    public void FindProblems_WhenTwoRowsArePrimary_ReportsIt()
    {
        var repository = Repository(
            primaryPointer: Storage3,
            (Storage3, RepositoryReplicaRole.Primary),
            (Storage1, RepositoryReplicaRole.Primary)
        );

        var problem = Assert.Single(RepositoryPrimaryConsistency.FindProblems(repository));
        Assert.Contains("2", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void FindProblems_WhenStorageNodeIdDiffersFromThePointer_ReportsIt()
    {
        var repository = Repository(primaryPointer: Storage3, (Storage3, RepositoryReplicaRole.Primary));
        repository.StorageNodeId = Storage1;

        var problem = Assert.Single(RepositoryPrimaryConsistency.FindProblems(repository));
        Assert.Contains("StorageNodeId", problem, StringComparison.Ordinal);
    }

    private static RepositoryEntity Repository(
        Guid? primaryPointer,
        params (Guid Node, RepositoryReplicaRole Role)[] rows
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            StorageNodeId = primaryPointer,
            PrimaryStorageNodeId = primaryPointer,
            Replicas = [.. rows.Select(row => new RepositoryReplicaEntity { StorageNodeId = row.Node, Role = row.Role })],
        };
}
