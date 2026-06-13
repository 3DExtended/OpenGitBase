using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class SingleModelQueryHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedModel()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubSingleModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        int entityId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "found" });
            await context.SaveChangesAsync();
            entityId = (await context.StubEntities.SingleAsync()).Id;
        }

        var handler = provider.GetRequiredService<StubSingleModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubSingleModelQuery { ModelId = StubIdentifier.From(entityId) },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal("found", result.Get().Name);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubSingleModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubSingleModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubSingleModelQuery { ModelId = StubIdentifier.From(123) },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_UsesAddIncludesOverride()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<IncludingSingleModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        int entityId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "included" });
            await context.SaveChangesAsync();
            entityId = (await context.StubEntities.SingleAsync()).Id;
        }

        var handler = provider.GetRequiredService<IncludingSingleModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubSingleModelQuery { ModelId = StubIdentifier.From(entityId) },
            CancellationToken.None
        );

        Assert.True(handler.AddIncludesCalled);
        Assert.Equal("included", result.Get().Name);
    }

    private sealed class IncludingSingleModelQueryHandler(
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
        >(mapper, contextFactory)
    {
        public bool AddIncludesCalled { get; private set; }

        protected override IQueryable<StubEntity> AddIncludes(IQueryable<StubEntity> queryable)
        {
            AddIncludesCalled = true;
            return queryable;
        }
    }
}
