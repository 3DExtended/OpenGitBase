namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubSingleModelQueryHandler(
    MapsterMapper.IMapper mapper,
    IDbContextFactory<StubDbContext> contextFactory
)
    : SingleModelQueryHandlerBase<
        StubSingleModelQuery,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(mapper, contextFactory);
