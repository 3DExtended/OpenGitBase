using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetRepositoryUsageQueryHandler
    : IQueryHandler<GetRepositoryUsageQuery, RepositoryUsageDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;
    private readonly IQueryProcessor _queryProcessor;

    public GetRepositoryUsageQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        RepositoryStorageQuotaOptions quotaOptions,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _quotaOptions = quotaOptions;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<RepositoryUsageDto>> RunQueryAsync(
        GetRepositoryUsageQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.RepositoryId.Value, cancellationToken);

        if (entity == null)
        {
            return Option<RepositoryUsageDto>.None;
        }

        var bytesLimit = entity.MaxBytesOverride
            ?? await ResolveBytesLimitAsync(entity.OwnerUserId, cancellationToken).ConfigureAwait(false);

        return Option.From(
            new RepositoryUsageDto
            {
                BytesUsed = entity.StorageBytesUsed,
                BytesLimit = bytesLimit,
                FileSizeLimit = _quotaOptions.MaxFileBytes,
            }
        );
    }

    private async Task<long> ResolveBytesLimitAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken
    )
    {
        var organizationResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationQuery { ModelId = OrganizationId.From(ownerUserId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            return _quotaOptions.MaxBytes;
        }

        var quotaResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationStorageQuotaQuery
                {
                    OrganizationId = OrganizationId.From(ownerUserId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return quotaResult.IsSome ? quotaResult.Get().BytesLimit : _quotaOptions.MaxBytes;
    }
}
