namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubUpdateCommandHandler(
    MapsterMapper.IMapper mapper,
    IDbContextFactory<StubDbContext> contextFactory
)
    : UpdateCommandHandlerBase<
        StubUpdateCommand,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(mapper, contextFactory);
