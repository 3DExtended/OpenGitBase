using System.Linq.Expressions;

namespace OpenGitBase.Cqrs.EfCore;

public abstract class SingleModelBySelectorQueryHandlerBase<
    TQuery,
    TModel,
    TIdentifier,
    TIdentifierValue,
    TContext,
    TEntity
> : IQueryHandler<TQuery, TModel>
    where TQuery : SingleModelBySelectorQuery<TModel, TIdentifier, TIdentifierValue, TQuery>
    where TModel : ModelBase<TIdentifier, TIdentifierValue>
    where TIdentifier : Identifier<TIdentifierValue, TIdentifier>, new()
    where TContext : DbContext
    where TEntity : class, IIdentifiableEntity<TIdentifierValue>
{
    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly MapsterMapper.IMapper _mapper;

    protected SingleModelBySelectorQueryHandlerBase(
        MapsterMapper.IMapper mapper,
        IDbContextFactory<TContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public abstract Expression<Func<TEntity, bool>> SelectorPredicate(TQuery query);

    public async Task<Option<TModel>> RunQueryAsync(
        TQuery query,
        CancellationToken cancellationToken
    )
    {
        using (
            var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false)
        )
        {
            var databaseQuery = AddIncludes(context.Set<TEntity>().AsNoTracking());

            var entity = await databaseQuery
                .FirstOrDefaultAsync(SelectorPredicate(query), cancellationToken)
                .ConfigureAwait(false);

            return entity == null ? Option.None : Option.From(_mapper.Map<TModel>(entity));
        }
    }

    /// <summary>
    /// Override this method to add '.Include(...)' calls for retrieving the entities.
    /// </summary>
    protected virtual IQueryable<TEntity> AddIncludes(IQueryable<TEntity> queryable) => queryable;
}
