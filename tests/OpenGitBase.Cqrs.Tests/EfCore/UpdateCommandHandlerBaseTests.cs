using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class UpdateCommandHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_UpdatesEntityInDatabase()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubUpdateCommandHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        StubEntity existing;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            existing = new StubEntity { Name = "before-update" };
            context.StubEntities.Add(existing);
            await context.SaveChangesAsync();
        }

        var handler = provider.GetRequiredService<StubUpdateCommandHandler>();
        var result = await handler.RunQueryAsync(
            new StubUpdateCommand
            {
                UpdatedModel = new StubModel
                {
                    Id = StubIdentifier.From(existing.Id),
                    Name = "after-update",
                },
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var updated = await verifyContext.StubEntities.SingleAsync(e => e.Id == existing.Id);
        Assert.Equal("after-update", updated.Name);
        Assert.Equal(Unit.Value, result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubUpdateCommandHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubUpdateCommandHandler>();
        var result = await handler.RunQueryAsync(
            new StubUpdateCommand
            {
                UpdatedModel = new StubModel { Id = StubIdentifier.From(999), Name = "missing" },
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
