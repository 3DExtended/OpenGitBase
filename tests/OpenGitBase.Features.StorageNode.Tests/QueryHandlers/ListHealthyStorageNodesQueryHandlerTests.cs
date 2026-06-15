using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.QueryHandlers;
using OpenGitBase.Features.StorageNode.Tests.Testing;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class ListHealthyStorageNodesQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ExcludesUnhealthyAndStaleNodes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = CreateServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context
                .Set<Entities.StorageNodeEntity>()
                .AddRange(
                    StorageNodeTestData.CreateEntity("healthy", true, 5_000_000),
                    StorageNodeTestData.CreateEntity("unhealthy", false, 9_000_000),
                    StorageNodeTestData.CreateEntity(
                        "stale",
                        true,
                        8_000_000,
                        DateTimeOffset.UtcNow.AddMinutes(-10)
                    )
                );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<ListHealthyStorageNodesQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListHealthyStorageNodesQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Single(result.Get());
        Assert.Equal("healthy", result.Get()[0].NodeId);
    }

    private static ServiceCollection CreateServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        services.AddSingleton(
            new StorageNodeOptions { MissedHeartbeatThresholdSeconds = 60 }
        );
        var mapsterConfig = new TypeAdapterConfig();
        new StorageNodeMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(ListHealthyStorageNodesQueryHandler).Assembly)
        );
        return services;
    }
}
