using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class SingleModelBySelectorQueryHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityMatchesSelector_ReturnsMappedModel()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubSingleModelBySelectorQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "selector-match" });
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<StubSingleModelBySelectorQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubSingleModelBySelectorQuery { Name = "selector-match" },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal("selector-match", result.Get().Name);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubSingleModelBySelectorQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubSingleModelBySelectorQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubSingleModelBySelectorQuery { Name = "missing" },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_UsesAddIncludesOverride()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<IncludingSingleModelBySelectorQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "included-selector" });
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<IncludingSingleModelBySelectorQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubSingleModelBySelectorQuery { Name = "included-selector" },
            CancellationToken.None
        );

        Assert.True(handler.AddIncludesCalled);
        Assert.Equal("included-selector", result.Get().Name);
    }

    private sealed class IncludingSingleModelBySelectorQueryHandler(
        MapsterMapper.IMapper mapper,
        IDbContextFactory<StubDbContext> contextFactory
    )
        : SingleModelBySelectorQueryHandlerBase<
            StubSingleModelBySelectorQuery,
            StubModel,
            StubIdentifier,
            int,
            StubDbContext,
            StubEntity
        >(mapper, contextFactory)
    {
        public bool AddIncludesCalled { get; private set; }

        public override System.Linq.Expressions.Expression<
            Func<StubEntity, bool>
        > SelectorPredicate(StubSingleModelBySelectorQuery query) =>
            entity => entity.Name == query.Name;

        protected override IQueryable<StubEntity> AddIncludes(IQueryable<StubEntity> queryable)
        {
            AddIncludesCalled = true;
            return queryable;
        }
    }
}
