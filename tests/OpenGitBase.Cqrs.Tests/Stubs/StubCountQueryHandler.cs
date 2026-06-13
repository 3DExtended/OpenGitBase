namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubCountQueryHandler(IDbContextFactory<StubDbContext> contextFactory)
    : CountQueryHandlerBase<
        StubCountQuery,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(contextFactory);
