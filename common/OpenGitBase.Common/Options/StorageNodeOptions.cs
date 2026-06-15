﻿namespace OpenGitBase.Common.Options;

public sealed class StorageNodeOptions
{
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public int MissedHeartbeatThresholdSeconds { get; set; } = 90;
}
