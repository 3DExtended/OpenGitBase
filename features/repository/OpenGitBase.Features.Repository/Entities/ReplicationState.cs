namespace OpenGitBase.Features.Repository.Entities;

public enum ReplicationState
{
    Rf1Backfilling = 0,
    Rf3Healthy = 1,
    Degraded = 2,
    Promoting = 3,
    Rf4Migrating = 4,
    Rf4Healthy = 5,
    Recovering = 6,
}
