using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Cqrs.Tests.Infrastructure;
using OpenGitBase.Cqrs.Tests.Stubs;

namespace OpenGitBase.Cqrs.Tests.EfCore;

public class DeleteCommandHandlerBaseTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_DeletesAndReturnsUnit()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubDeleteCommandHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var contextFactory = provider.GetRequiredService<IDbContextFactory<StubDbContext>>();
        int entityId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.StubEntities.Add(new StubEntity { Name = "delete-me" });
            await context.SaveChangesAsync();
            entityId = (await context.StubEntities.SingleAsync()).Id;
        }

        var handler = provider.GetRequiredService<StubDeleteCommandHandler>();
        var result = await handler.RunQueryAsync(
            new StubDeleteCommand { Id = StubIdentifier.From(entityId) },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(Unit.Value, result.Get());

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        Assert.Empty(verifyContext.StubEntities);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        var (connection, provider) = await SqliteDbContextFactory.CreateAsync(services =>
            services.AddTransient<StubDeleteCommandHandler>()
        );
        await using var sqliteConnection = connection;
        await using var serviceProvider = provider;

        var handler = provider.GetRequiredService<StubDeleteCommandHandler>();
        var result = await handler.RunQueryAsync(
            new StubDeleteCommand { Id = StubIdentifier.From(404) },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }
}
