namespace OpenGitBase.Cqrs.Tests.Stubs;

public sealed class StubDeleteCommandHandler(IDbContextFactory<StubDbContext> contextFactory)
    : DeleteCommandHandlerBase<
        StubDeleteCommand,
        StubModel,
        StubIdentifier,
        int,
        StubDbContext,
        StubEntity
    >(contextFactory);
