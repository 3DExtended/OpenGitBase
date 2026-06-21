﻿using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class ListAdminRepositoryReplicationQuery
    : IQuery<ListAdminRepositoryReplicationResult, ListAdminRepositoryReplicationQuery>
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public AdminRepositoryReplicationSort Sort { get; init; } =
        AdminRepositoryReplicationSort.Severity;

    public string? Search { get; init; }

    public ReplicationAttentionPreset Attention { get; init; } = ReplicationAttentionPreset.All;
}
