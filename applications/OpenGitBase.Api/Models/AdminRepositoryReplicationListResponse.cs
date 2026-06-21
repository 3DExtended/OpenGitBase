using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class AdminRepositoryReplicationListResponse
{
    public IReadOnlyList<AdminRepositoryReplicationSummaryDto> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }
}
