using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class CreateQueryHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_DefaultAfterCreation_IsInvoked()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubCreateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubCreateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateQuery { ModelToCreate = new StubModel { Name = "default-hook" } },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
    }

    [Fact]
    public async Task RunQueryAsync_CreatesEntityAndReturnsIdentifier()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubCreateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubCreateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateQuery { ModelToCreate = new StubModel { Name = "created" } },
            CancellationToken.None
        );

        Assert.True(result.IsSome);

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.StubEntities.FindAsync(result.Get().Value);

        Assert.NotNull(entity);
        Assert.Equal("created", entity.Name);
    }

    [Fact]
    public async Task RunQueryAsync_WhenPrepareModelReturnsNone_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<RejectingCreateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<RejectingCreateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateQuery { ModelToCreate = new StubModel { Name = "rejected" } },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_InvokesAfterCreationHook()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<TrackingCreateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<TrackingCreateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateQuery { ModelToCreate = new StubModel { Name = "tracked" } },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.True(handler.AfterCreationCalled);
    }

    private sealed class RejectingCreateQueryHandler(
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
        >(mapper, contextFactory)
    {
        protected override Task<Option<StubModel>> PrepareModelAsync(
            StubModel model,
            StubDbContext context,
            CancellationToken cancellationToken
        ) => Task.FromResult(Option<StubModel>.None);
    }

    private sealed class TrackingCreateQueryHandler(
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
        >(mapper, contextFactory)
    {
        public bool AfterCreationCalled { get; private set; }

        protected override Task AfterCreationAsync(
            StubCreateQuery query,
            StubIdentifier id,
            CancellationToken cancellationToken
        )
        {
            AfterCreationCalled = true;
            return Task.CompletedTask;
        }
    }
}
