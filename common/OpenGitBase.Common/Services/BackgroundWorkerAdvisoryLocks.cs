namespace OpenGitBase.Common.Services;

/// <summary>
/// Well-known Postgres advisory-lock keys used to serialize a background worker's batch
/// across API replicas, via <see cref="PostgresAdvisoryLockService"/>. Each worker that polls
/// and mutates a shared queue needs its own key so only one replica processes a batch at a
/// time - without it, every replica claims the same rows on every tick.
/// </summary>
public static class BackgroundWorkerAdvisoryLocks
{
    public const long JobDispatchCoordinator = 84007301;

    public const long DependencyLayerPromotionWorker = 84007302;

    public const long JobTimeoutEnforcer = 84007303;
}
