﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class RepositoryUsageDto
{
    public long BytesUsed { get; set; }

    public long BytesLimit { get; set; }

    public long FileSizeLimit { get; set; }
}
