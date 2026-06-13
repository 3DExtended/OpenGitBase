using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class CountQueryHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsEntityCount()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubCountQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.AddRange(
                new StubEntity { Name = "one" },
                new StubEntity { Name = "two" },
                new StubEntity { Name = "three" }
            );
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<StubCountQueryHandler>();
        var result = await handler.RunQueryAsync(new StubCountQuery(), CancellationToken.None);

        Assert.True(result.IsSome);
        Assert.Equal(3, result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_UsesPrepareQueryOverride()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<FilteredCountQueryHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.AddRange(
                new StubEntity { Name = "counted" },
                new StubEntity { Name = "ignored" }
            );
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<FilteredCountQueryHandler>();
        var result = await handler.RunQueryAsync(new StubCountQuery(), CancellationToken.None);

        Assert.True(handler.PrepareQueryCalled);
        Assert.Equal(1, result.Get());
    }

    private sealed class FilteredCountQueryHandler(IDbContextFactory<StubDbContext> contextFactory)
        : CountQueryHandlerBase<
            StubCountQuery,
            StubModel,
            StubIdentifier,
            int,
            StubDbContext,
            StubEntity
        >(contextFactory)
    {
        public bool PrepareQueryCalled { get; private set; }

        protected override IQueryable<StubEntity> PrepareQuery(IQueryable<StubEntity> efquery)
        {
            PrepareQueryCalled = true;
            return efquery.Where(entity => entity.Name == "counted");
        }
    }
}
