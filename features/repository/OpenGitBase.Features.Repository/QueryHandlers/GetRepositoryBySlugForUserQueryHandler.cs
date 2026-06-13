using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetRepositoryBySlugForUserQueryHandler
    : IQueryHandler<GetRepositoryBySlugForUserQuery, RepositoryDto>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetRepositoryBySlugForUserQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<RepositoryDto>> RunQueryAsync(
        GetRepositoryBySlugForUserQuery query,
        CancellationToken cancellationToken
    )
    {
        using (
            var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var databaseQuery = context.Set<RepositoryEntity>().AsNoTracking();

            var entity = await databaseQuery
                .FirstOrDefaultAsync(
                    cp => cp.Slug == query.Slug && cp.OwnerUserId == query.OwnerUserId.Value,
                    cancellationToken
                )
                .ConfigureAwait(false);

            return entity == null ? Option.None : Option.From(_mapper.Map<RepositoryDto>(entity));
        }
    }
}
