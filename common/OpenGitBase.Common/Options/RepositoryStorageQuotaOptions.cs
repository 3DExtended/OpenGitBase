﻿namespace OpenGitBase.Common.Options;

public class RepositoryStorageQuotaOptions
{
    public bool Enabled { get; set; } = true;

    public long MaxBytes { get; set; } = 1_073_741_824;

    public long MaxFileBytes { get; set; } = 52_428_800;
}
