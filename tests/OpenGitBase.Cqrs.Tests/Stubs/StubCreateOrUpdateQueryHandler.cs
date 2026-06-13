using System.Linq.Expressions;

namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubCreateOrUpdateQueryHandler(
    MapsterMapper.IMapper mapper,
    IDbContextFactory<StubDbContext> contextFactory
)
    : CreateOrUpdateIfExistingQueryHandlerBase<
        StubCreateOrUpdateQuery,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(mapper, contextFactory)
{
    protected override Expression<Func<StubEntity, bool>> GetPossiblyExistingEntity(
        StubCreateOrUpdateQuery query
    ) => entity => entity.Name == query.MatchName;
}
