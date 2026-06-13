using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class ListOfModelQueryHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_WithoutIds_ReturnsAllEntities()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubListOfModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.AddRange(
                new StubEntity { Name = "one" },
                new StubEntity { Name = "two" }
            );
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<StubListOfModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubListOfModelQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(2, result.Get().Count);
    }

    [Fact]
    public async Task RunQueryAsync_WithAllIdsFound_ReturnsModels()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubListOfModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        List<int> ids;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.AddRange(
                new StubEntity { Name = "a" },
                new StubEntity { Name = "b" }
            );
            await context.SaveChangesAsync();
            ids = await context.StubEntities.Select(e => e.Id).ToListAsync();
        }

        var handler = provider.GetRequiredService<StubListOfModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubListOfModelQuery
            {
                Ids = Option.From<IReadOnlyList<StubIdentifier>>(
                    ids.Select(id => StubIdentifier.From(id)).ToList()
                ),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(2, result.Get().Count);
    }

    [Fact]
    public async Task RunQueryAsync_WithMissingIdsAndPartialAllowed_ReturnsPartialSet()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubListOfModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        int existingId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "only" });
            await context.SaveChangesAsync();
            existingId = (await context.StubEntities.SingleAsync()).Id;
        }

        var handler = provider.GetRequiredService<StubListOfModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubListOfModelQuery
            {
                AllowPartialResultSet = true,
                Ids = Option.From<IReadOnlyList<StubIdentifier>>(
                    new List<StubIdentifier>
                    {
                        StubIdentifier.From(existingId),
                        StubIdentifier.From(999),
                    }
                ),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Single(result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_WithMissingIdsAndPartialDisallowed_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubListOfModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        int existingId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "only" });
            await context.SaveChangesAsync();
            existingId = (await context.StubEntities.SingleAsync()).Id;
        }

        var handler = provider.GetRequiredService<StubListOfModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubListOfModelQuery
            {
                AllowPartialResultSet = false,
                Ids = Option.From<IReadOnlyList<StubIdentifier>>(
                    new List<StubIdentifier>
                    {
                        StubIdentifier.From(existingId),
                        StubIdentifier.From(999),
                    }
                ),
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_UsesAddIncludesAndAddWhereOverrides()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<FilteringListOfModelQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.AddRange(
                new StubEntity { Name = "keep" },
                new StubEntity { Name = "drop" }
            );
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<FilteringListOfModelQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StubListOfModelQuery(),
            CancellationToken.None
        );

        Assert.True(handler.AddIncludesCalled);
        Assert.True(handler.AddWhereCalled);
        Assert.Single(result.Get());
        Assert.Equal("keep", result.Get().Single().Name);
    }

    private sealed class FilteringListOfModelQueryHandler(
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
        >(mapper, contextFactory)
    {
        public bool AddIncludesCalled { get; private set; }

        public bool AddWhereCalled { get; private set; }

        protected override IQueryable<StubEntity> AddIncludes(IQueryable<StubEntity> queryable)
        {
            AddIncludesCalled = true;
            return queryable;
        }

        protected override IQueryable<StubEntity> AddWhere(
            IQueryable<StubEntity> queryable,
            StubListOfModelQuery query
        )
        {
            AddWhereCalled = true;
            return queryable.Where(entity => entity.Name == "keep");
        }
    }
}
