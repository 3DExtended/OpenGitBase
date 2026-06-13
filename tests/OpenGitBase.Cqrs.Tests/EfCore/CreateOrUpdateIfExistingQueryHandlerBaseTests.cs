using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class CreateOrUpdateIfExistingQueryHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityDoesNotExist_CreatesEntity()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubCreateOrUpdateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubCreateOrUpdateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateOrUpdateQuery
            {
                ModelToCreate = new StubModel { Name = "new-entity" },
                MatchName = "new-entity",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context.StubEntities.SingleAsync();

        Assert.Equal("new-entity", entity.Name);
        Assert.True(entity.Id > 0);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesEntity()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubCreateOrUpdateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        int existingId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "existing" });
            await context.SaveChangesAsync();
            existingId = (await context.StubEntities.SingleAsync()).Id;
        }

        var handler = provider.GetRequiredService<StubCreateOrUpdateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateOrUpdateQuery
            {
                ModelToCreate = new StubModel
                {
                    Id = StubIdentifier.From(existingId),
                    Name = "updated",
                },
                MatchName = "existing",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(existingId, result.Get().Value);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var entity = await verifyContext.StubEntities.SingleAsync();
        Assert.Equal(existingId, entity.Id);
        Assert.Equal("updated", entity.Name);
    }

    [Fact]
    public async Task RunQueryAsync_WhenPrepareModelReturnsNone_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<RejectingCreateOrUpdateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<RejectingCreateOrUpdateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateOrUpdateQuery
            {
                ModelToCreate = new StubModel { Name = "rejected" },
                MatchName = "rejected",
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_InvokesAfterCreationHook()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<TrackingCreateOrUpdateQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<TrackingCreateOrUpdateQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubCreateOrUpdateQuery
            {
                ModelToCreate = new StubModel { Name = "tracked" },
                MatchName = "tracked",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.True(handler.AfterCreationCalled);
    }

    private sealed class RejectingCreateOrUpdateQueryHandler(
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
        protected override System.Linq.Expressions.Expression<
            Func<StubEntity, bool>
        > GetPossiblyExistingEntity(StubCreateOrUpdateQuery query) =>
            entity => entity.Name == query.MatchName;

        protected override Task<Option<StubModel>> PrepareModelAsync(
            StubModel modelToCreate,
            StubDbContext context,
            CancellationToken cancellationToken
        ) => Task.FromResult(Option<StubModel>.None);
    }

    private sealed class TrackingCreateOrUpdateQueryHandler(
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
        public bool AfterCreationCalled { get; private set; }

        protected override System.Linq.Expressions.Expression<
            Func<StubEntity, bool>
        > GetPossiblyExistingEntity(StubCreateOrUpdateQuery query) =>
            entity => entity.Name == query.MatchName;

        protected override Task AfterCreationAsync(
            StubCreateOrUpdateQuery query,
            StubIdentifier id,
            CancellationToken cancellationToken
        )
        {
            AfterCreationCalled = true;
            return Task.CompletedTask;
        }
    }
}
