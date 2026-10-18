﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class RepositoryUsageDto
{
    /// <summary>Everything that counts against the limit: <see cref="GitBytes"/> plus <see cref="LfsBytes"/>.</summary>
    public long BytesUsed { get; set; }

    /// <summary>The git repository itself.</summary>
    public long GitBytes { get; set; }

    /// <summary>Git LFS objects, each counted once regardless of how many copies storage keeps.</summary>
    public long LfsBytes { get; set; }

    public long BytesLimit { get; set; }

    public long FileSizeLimit { get; set; }
}
