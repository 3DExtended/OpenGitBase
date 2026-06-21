using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Services;

public sealed class ListAdminRepositoryReplicationQueryHandler
    : IQueryHandler<ListAdminRepositoryReplicationQuery, ListAdminRepositoryReplicationResult>
{
    private const int MaxPageSize = 100;

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListAdminRepositoryReplicationQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<ListAdminRepositoryReplicationResult>> RunQueryAsync(
        ListAdminRepositoryReplicationQuery query,
        CancellationToken cancellationToken
    )
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var repositories = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nodeIds = repositories
            .SelectMany(repository => repository.Replicas.Select(replica => replica.StorageNodeId))
            .Concat(
                repositories
                    .Where(repository => repository.PrimaryStorageNodeId.HasValue)
                    .Select(repository => repository.PrimaryStorageNodeId!.Value)
            )
            .Concat(
                repositories
                    .Where(repository => repository.StorageNodeId.HasValue)
                    .Select(repository => repository.StorageNodeId!.Value)
            )
            .Distinct()
            .ToList();

        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => nodeIds.Contains(node.Id))
            .ToDictionaryAsync(node => node.Id, cancellationToken)
            .ConfigureAwait(false);

        var ownerIds = repositories.Select(repository => repository.OwnerUserId).Distinct().ToList();
        var ownerSlugs = await ResolveOwnerSlugsAsync(context, ownerIds, cancellationToken)
            .ConfigureAwait(false);

        var summaries = repositories
            .Select(repository => BuildSummary(repository, nodes, ownerSlugs))
            .Where(summary => ReplicationAttention.MatchesPreset(summary, query.Attention))
            .Where(summary => MatchesSearch(summary, query.Search))
            .ToList();

        summaries = SortSummaries(summaries, query.Sort);

        var totalCount = summaries.Count;
        var items = summaries.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Option.From(
            new ListAdminRepositoryReplicationResult
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            }
        );
    }

    private static bool MatchesSearch(
        AdminRepositoryReplicationSummaryDto summary,
        string? search
    )
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var term = search.Trim();
        return summary.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
            || summary.OwnerSlug.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static List<AdminRepositoryReplicationSummaryDto> SortSummaries(
        List<AdminRepositoryReplicationSummaryDto> summaries,
        AdminRepositoryReplicationSort sort
    ) =>
        sort switch
        {
            AdminRepositoryReplicationSort.Name => summaries
                .OrderBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AdminRepositoryReplicationSort.Lag => summaries
                .OrderByDescending(summary => summary.MaxWatermarkLag)
                .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AdminRepositoryReplicationSort.State => summaries
                .OrderBy(summary => summary.ReplicationState, StringComparer.Ordinal)
                .ThenBy(summary => summary.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => summaries
                .OrderBy(summary => summary, Comparer<AdminRepositoryReplicationSummaryDto>.Create(
                    ReplicationAttention.CompareSeverity
                ))
                .ToList(),
        };

    private static AdminRepositoryReplicationSummaryDto BuildSummary(
        RepositoryEntity repository,
        IReadOnlyDictionary<Guid, StorageNodeEntity> nodes,
        IReadOnlyDictionary<Guid, string> ownerSlugs
    )
    {
        var primaryNodeId = repository.PrimaryStorageNodeId ?? repository.StorageNodeId;
        var primaryNodeKey = string.Empty;
        if (primaryNodeId.HasValue && nodes.TryGetValue(primaryNodeId.Value, out var primaryNode))
        {
            primaryNodeKey = primaryNode.NodeId;
        }

        var maxLag = repository.Replicas.Count == 0
            ? 0
            : repository.Replicas.Max(replica =>
                Math.Max(0, repository.PrimaryWatermark - replica.AppliedWatermark)
            );

        var healthyCount = repository.Replicas.Count(replica =>
            nodes.TryGetValue(replica.StorageNodeId, out var node) && node.IsHealthy
        );

        var writeQuorumAvailable = primaryNodeId is null
            ? repository.StorageNodeId.HasValue
                && nodes.TryGetValue(repository.StorageNodeId.Value, out var legacyNode)
                && legacyNode.IsHealthy
            : healthyCount >= 2;

        ownerSlugs.TryGetValue(repository.OwnerUserId, out var ownerSlug);

        return new AdminRepositoryReplicationSummaryDto
        {
            RepositoryId = repository.Id,
            Name = repository.Name,
            OwnerSlug = ownerSlug ?? string.Empty,
            ReplicationState = repository.ReplicationState.ToString(),
            ReplicaCount = repository.Replicas.Count,
            PrimaryNodeId = primaryNodeKey,
            PrimaryWatermark = repository.PrimaryWatermark,
            MaxWatermarkLag = maxLag,
            WriteQuorumAvailable = writeQuorumAvailable,
            ReplicationEpoch = repository.ReplicationEpoch,
            OldestLastSyncedAt = repository.Replicas
                .Select(replica => replica.LastSyncedAt)
                .Where(timestamp => timestamp.HasValue)
                .Min(),
        };
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ResolveOwnerSlugsAsync(
        OpenGitBaseDbContext context,
        IReadOnlyList<Guid> ownerIds,
        CancellationToken cancellationToken
    )
    {
        var slugs = new Dictionary<Guid, string>();

        if (context.Model.FindEntityType(typeof(UserEntity)) != null)
        {
            var users = await context
                .Set<UserEntity>()
                .AsNoTracking()
                .Where(user => ownerIds.Contains(user.Id))
                .Select(user => new { user.Id, user.Username })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var user in users)
            {
                slugs[user.Id] = user.Username;
            }
        }

        if (context.Model.FindEntityType(typeof(OrganizationEntity)) != null)
        {
            var organizations = await context
                .Set<OrganizationEntity>()
                .AsNoTracking()
                .Where(org => ownerIds.Contains(org.Id))
                .Select(org => new { org.Id, org.Slug })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var organization in organizations)
            {
                slugs[organization.Id] = organization.Slug;
            }
        }

        return slugs;
    }
}
