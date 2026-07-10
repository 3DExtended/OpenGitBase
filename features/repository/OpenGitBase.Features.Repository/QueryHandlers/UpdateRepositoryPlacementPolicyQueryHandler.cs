using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public sealed class UpdateRepositoryPlacementPolicyQueryHandler
    : IQueryHandler<UpdateRepositoryPlacementPolicyQuery, RepositoryDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public UpdateRepositoryPlacementPolicyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<RepositoryDto>> RunQueryAsync(
        UpdateRepositoryPlacementPolicyQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context
            .Set<RepositoryEntity>()
            .FirstOrDefaultAsync(repository => repository.Id == query.RepositoryId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<RepositoryDto>.None;
        }

        entity.PlacementPolicy = query.PlacementPolicy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<RepositoryDto>(entity));
    }
}
