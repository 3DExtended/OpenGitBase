namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubListOfModelQueryHandler(
    MapsterMapper.IMapper mapper,
    IDbContextFactory<StubDbContext> contextFactory
)
    : ListOfModelQueryHandlerBase<
        StubListOfModelQuery,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(mapper, contextFactory);
