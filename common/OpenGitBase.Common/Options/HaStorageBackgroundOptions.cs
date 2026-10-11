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

    /// <summary>
    /// How long one upload of an encrypted replication artifact to one encrypted replica may
    /// take before that replica counts as having refused it (push path and reconciler). Well
    /// below HAProxy's 5-minute server timeout, which a push's quorum-replicate call runs under,
    /// so a replica that accepts connections but never answers cannot fail a push that another
    /// replica has already stored.
    /// </summary>
    public int ArtifactUploadTimeoutSeconds { get; set; } = 120;
}
