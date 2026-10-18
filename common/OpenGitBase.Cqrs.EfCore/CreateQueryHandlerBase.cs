namespace OpenGitBase.Cqrs.EfCore;

public abstract class CreateQueryHandlerBase<
    TQuery,
    TModel,
    TIdentifier,
    TIdentifierValue,
    TContext,
    TEntity
> : IQueryHandler<TQuery, TIdentifier>
    where TQuery : CreateQuery<TModel, TIdentifier, TIdentifierValue, TQuery>
    where TModel : ModelBase<TIdentifier, TIdentifierValue>
    where TIdentifier : Identifier<TIdentifierValue, TIdentifier>, new()
    where TContext : DbContext
    where TEntity : class, IIdentifiableEntity<TIdentifierValue>
{
    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly MapsterMapper.IMapper _mapper;

    protected CreateQueryHandlerBase(
        MapsterMapper.IMapper mapper,
        IDbContextFactory<TContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<TIdentifier>> RunQueryAsync(
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
            var preparedModel = await PrepareModelAsync(
                    query.ModelToCreate,
                    context,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (preparedModel.IsNone)
            {
                return Option.None;
            }

            // Mapped once and added again on a replay, so every attempt inserts the same key.
            var entity = _mapper.Map<TEntity>(preparedModel.Get());
            await context
                .InsertOnceAsync(
                    async ct =>
                    {
                        await context.Set<TEntity>().AddAsync(entity, ct).ConfigureAwait(false);
                        return await context.SaveChangesAsync(ct).ConfigureAwait(false);
                    },
                    ct => context.Set<TEntity>().AnyAsync(e => e.Id!.Equals(entity.Id), ct),
                    cancellationToken
                )
                .ConfigureAwait(false);

            var id = Identifier<TIdentifierValue, TIdentifier>.From(entity.Id);
            await AfterCreationAsync(query, id, cancellationToken).ConfigureAwait(false);
            return id;
        }
    }

    protected virtual Task AfterCreationAsync(
        TQuery query,
        TIdentifier id,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected virtual Task<Option<TModel>> PrepareModelAsync(
        TModel model,
        TContext context,
        CancellationToken cancellationToken
    ) => Task.FromResult(Option.From(model));
}
