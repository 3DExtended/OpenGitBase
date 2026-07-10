using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed record ReplicaSetSelection(
    StorageNodeDto Primary,
    StorageNodeDto ReadReplica,
    StorageNodeDto EncryptedReplicaA,
    StorageNodeDto EncryptedReplicaB
)
{
    public StorageNodeDto ReplicaA => ReadReplica;

    public StorageNodeDto ReplicaB => EncryptedReplicaA;

    public IReadOnlyList<(StorageNodeDto Node, RepositoryReplicaRole Role)> ProvisionTargets
    {
        get
        {
            var targets = new List<(StorageNodeDto, RepositoryReplicaRole)>
            {
                (Primary, RepositoryReplicaRole.Primary),
            };

            if (ReadReplica.Id != Primary.Id)
            {
                targets.Add((ReadReplica, RepositoryReplicaRole.ReadReplica));
            }

            targets.Add((EncryptedReplicaA, RepositoryReplicaRole.EncryptedReplica));
            targets.Add((EncryptedReplicaB, RepositoryReplicaRole.EncryptedReplica));
            return targets;
        }
    }

    public IReadOnlyList<StorageNodeDto> AllNodes =>
        ProvisionTargets
            .Select(target => target.Node)
            .GroupBy(node => node.Id.Value)
            .Select(group => group.First())
            .ToList();
}
