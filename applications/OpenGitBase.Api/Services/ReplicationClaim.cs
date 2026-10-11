namespace OpenGitBase.Api.Services;

/// <summary>
/// Timestamps written to the primary replica's LastSyncedAt to mark "replication of this
/// repository is in progress". Truncated to whole milliseconds so a value read back from the
/// database (PostgreSQL keeps microseconds, .NET ticks are 100 ns) compares equal to the value
/// that was written.
/// </summary>
internal static class ReplicationClaim
{
    public static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero);
    }
}
