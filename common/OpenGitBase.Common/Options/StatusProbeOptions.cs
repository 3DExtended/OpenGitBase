namespace OpenGitBase.Common.Options;

public sealed class StatusProbeOptions
{
    public bool Enabled { get; set; } = true;

    public int IntervalSeconds { get; set; } = 30;

    public int TimeoutMs { get; set; } = 5000;

    public int SlowThresholdMs { get; set; } = 2000;

    public int HistoryRetentionDays { get; set; } = 90;

    public long AdvisoryLockKey { get; set; } = 84007201;
}
