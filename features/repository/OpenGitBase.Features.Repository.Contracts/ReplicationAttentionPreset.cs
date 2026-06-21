﻿namespace OpenGitBase.Features.Repository.Contracts;

public enum ReplicationAttentionPreset
{
    All,
    Backfilling,
    Degraded,
    Lagging,
    NoQuorum,
}
