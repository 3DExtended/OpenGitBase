using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetRepositoryUsageQueryHandler
    : IQueryHandler<GetRepositoryUsageQuery, RepositoryUsageDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public GetRepositoryUsageQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        RepositoryStorageQuotaOptions quotaOptions
    )
    {
        _contextFactory = contextFactory;
        _quotaOptions = quotaOptions;
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

        return Option.From(
            new RepositoryUsageDto
            {
                BytesUsed = entity.StorageBytesUsed,
                BytesLimit = _quotaOptions.MaxBytes,
                FileSizeLimit = _quotaOptions.MaxFileBytes,
            }
        );
    }
}
