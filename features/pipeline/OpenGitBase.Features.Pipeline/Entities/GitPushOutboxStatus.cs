namespace OpenGitBase.Features.Pipeline.Entities;

public enum GitPushOutboxStatus
{
    Pending = 0,
    Processed = 1,

    // Scheduling failed the configured max-attempt count in a row. Left in place for
    // operator inspection instead of being retried forever every 2s.
    DeadLettered = 2,
}
