using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class GetRepositoryQueryHandler
    : SingleModelQueryHandlerBase<
        GetRepositoryQuery,
        RepositoryDto,
        RepositoryId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryEntity
    >
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public GetRepositoryQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public new async Task<Option<RepositoryDto>> RunQueryAsync(
        GetRepositoryQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await AddIncludes(context.Set<RepositoryEntity>().AsNoTracking())
            .FirstOrDefaultAsync(item => item.Id == query.ModelId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            return Option.None;
        }

        var dto = _mapper.Map<RepositoryDto>(entity);
        await RepositoryOwnerMetadataEnricher
            .EnrichAsync([dto], context, cancellationToken)
            .ConfigureAwait(false);
        return Option.From(dto);
    }
}
