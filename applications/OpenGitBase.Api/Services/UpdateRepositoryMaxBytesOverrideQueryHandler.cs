using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public sealed class UpdateRepositoryMaxBytesOverrideQueryHandler
    : IQueryHandler<UpdateRepositoryMaxBytesOverrideQuery, RepositoryDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IRepositoryByteOverrideService _byteOverrideService;

    public UpdateRepositoryMaxBytesOverrideQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        IRepositoryByteOverrideService byteOverrideService
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _byteOverrideService = byteOverrideService;
    }

    public async Task<Option<RepositoryDto>> RunQueryAsync(
        UpdateRepositoryMaxBytesOverrideQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.MaxBytesOverride is <= 0)
        {
            query.MaxBytesOverride = null;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .FirstOrDefaultAsync(repository => repository.Id == query.RepositoryId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<RepositoryDto>.None;
        }

        if (query.MaxBytesOverride is not null)
        {
            var eligibility = await _byteOverrideService
                .EvaluateAsync(entity, cancellationToken)
                .ConfigureAwait(false);
            if (!eligibility.Eligible)
            {
                return Option<RepositoryDto>.None;
            }

            if (query.MaxBytesOverride > eligibility.MaxAllowedOverride)
            {
                return Option<RepositoryDto>.None;
            }
        }

        entity.MaxBytesOverride = query.MaxBytesOverride;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<RepositoryDto>(entity));
    }
}
