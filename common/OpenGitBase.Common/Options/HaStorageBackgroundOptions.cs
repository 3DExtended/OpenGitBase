namespace OpenGitBase.Common.Options;

public sealed class HaStorageBackgroundOptions
{
    public bool Enabled { get; set; } = true;

    public int FailoverIntervalSeconds { get; set; } = 30;

    public int BackfillIntervalSeconds { get; set; } = 60;

    public int RebalanceIntervalSeconds { get; set; } = 60;

    public int ReconcilerIntervalSeconds { get; set; } = 900;

    /// <summary>
    /// How long the anti-entropy reconciler leaves a primary that is ahead of the quorum
    /// (AppliedWatermark above PrimaryWatermark) to the push that is replicating it, measured from
    /// the primary replica's LastSyncedAt. After that the write counts as failed and the
    /// reconciler replicates it itself.
    /// </summary>
    public int PrimaryAheadGraceSeconds { get; set; } = 600;
}
