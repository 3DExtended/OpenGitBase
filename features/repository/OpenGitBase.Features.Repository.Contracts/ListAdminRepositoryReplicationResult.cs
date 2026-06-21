﻿namespace OpenGitBase.Features.Repository.Contracts;

public sealed class ListAdminRepositoryReplicationResult
{
    public IReadOnlyList<AdminRepositoryReplicationSummaryDto> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
