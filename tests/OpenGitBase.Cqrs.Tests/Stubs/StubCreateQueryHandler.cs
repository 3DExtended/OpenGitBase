namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubCreateQueryHandler(
    MapsterMapper.IMapper mapper,
    IDbContextFactory<StubDbContext> contextFactory
)
    : CreateQueryHandlerBase<
        StubCreateQuery,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(mapper, contextFactory);
