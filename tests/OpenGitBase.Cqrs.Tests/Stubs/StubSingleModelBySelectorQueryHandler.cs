using System.Linq.Expressions;

namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubSingleModelBySelectorQueryHandler(
    MapsterMapper.IMapper mapper,
    IDbContextFactory<StubDbContext> contextFactory
)
    : SingleModelBySelectorQueryHandlerBase<
        StubSingleModelBySelectorQuery,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(mapper, contextFactory)
{
    public override Expression<Func<StubEntity, bool>> SelectorPredicate(
        StubSingleModelBySelectorQuery query
    ) => entity => entity.Name == query.Name;
}
