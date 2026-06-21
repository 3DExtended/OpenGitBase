namespace OpenGitBase.Common.Options;

public sealed class HaStorageBackgroundOptions
{
    public bool Enabled { get; set; } = true;

    public int FailoverIntervalSeconds { get; set; } = 30;

    public int BackfillIntervalSeconds { get; set; } = 60;

    public int RebalanceIntervalSeconds { get; set; } = 60;

    public int ReconcilerIntervalSeconds { get; set; } = 900;
}
