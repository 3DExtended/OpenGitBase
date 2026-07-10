namespace OpenGitBase.Features.Repository.Entities;

public enum RepositoryReplicaRole
{
    Primary = 0,
    Replica = 1,
    ReadReplica = 2,
    EncryptedReplica = 3,
}
