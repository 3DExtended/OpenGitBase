using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public sealed class GetRepositoryByteOverrideEligibilityQueryHandler
    : IQueryHandler<GetRepositoryByteOverrideEligibilityQuery, RepositoryByteOverrideEligibilityDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IRepositoryByteOverrideService _byteOverrideService;

    public GetRepositoryByteOverrideEligibilityQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IRepositoryByteOverrideService byteOverrideService
    )
    {
        _contextFactory = contextFactory;
        _byteOverrideService = byteOverrideService;
    }

    public async Task<Option<RepositoryByteOverrideEligibilityDto>> RunQueryAsync(
        GetRepositoryByteOverrideEligibilityQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .AsNoTracking()
            .FirstOrDefaultAsync(repository => repository.Id == query.RepositoryId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<RepositoryByteOverrideEligibilityDto>.None;
        }

        return Option.From(await _byteOverrideService.EvaluateAsync(entity, cancellationToken).ConfigureAwait(false));
    }
}
