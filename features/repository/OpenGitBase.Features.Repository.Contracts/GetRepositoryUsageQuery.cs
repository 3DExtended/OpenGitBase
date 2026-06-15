﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public class GetRepositoryUsageQuery : IQuery<RepositoryUsageDto, GetRepositoryUsageQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;
}
