using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public sealed class GetOrganizationStorageQuotaQueryHandler
    : IQueryHandler<GetOrganizationStorageQuotaQuery, OrganizationStorageQuotaDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;
    private readonly StorageNodeOptions _storageNodeOptions;

    public GetOrganizationStorageQuotaQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        RepositoryStorageQuotaOptions quotaOptions,
        StorageNodeOptions storageNodeOptions
    )
    {
        _contextFactory = contextFactory;
        _quotaOptions = quotaOptions;
        _storageNodeOptions = storageNodeOptions;
    }

    public async Task<Option<OrganizationStorageQuotaDto>> RunQueryAsync(
        GetOrganizationStorageQuotaQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var organizationExists = await context
            .Set<OrganizationEntity>()
            .AsNoTracking()
            .AnyAsync(
                organization => organization.Id == query.OrganizationId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!organizationExists)
        {
            return Option<OrganizationStorageQuotaDto>.None;
        }

        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_storageNodeOptions.MissedHeartbeatThresholdSeconds);
        var contributedCapacity = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node =>
                node.OwnerOrganizationId == query.OrganizationId.Value
                && node.IsHealthy
                && node.MaxBytes > 0
                && node.LastHeartbeatAt != null
                && node.LastHeartbeatAt >= cutoff
            )
            .SumAsync(node => node.MaxBytes, cancellationToken)
            .ConfigureAwait(false);

        return Option.From(
            new OrganizationStorageQuotaDto
            {
                PlatformBytesLimit = _quotaOptions.MaxBytes,
                ContributedBytesCapacity = contributedCapacity,
                BytesLimit = _quotaOptions.MaxBytes + contributedCapacity,
            }
        );
    }
}
