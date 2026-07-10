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
using OpenGitBase.Features.Status;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;
using OpenGitBase.Features.Status.QueryHandlers;

namespace OpenGitBase.Features.Status.Tests.QueryHandlers;

public class ListFleetComponentsQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_MarksStaleComponentsUnhealthy()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new FleetComponentOptions { MissedHeartbeatThresholdSeconds = 90 };
        await using var provider = CreateProvider(connection, options);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.Set<FleetComponentEntity>().Add(
                new FleetComponentEntity
                {
                    Id = Guid.NewGuid(),
                    ComponentType = FleetComponentType.Api,
                    InstanceId = "api-stale",
                    ProbeUrl = "http://api-stale:8080/health",
                    RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                    LastHeartbeatAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    IsHealthy = true,
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<ListFleetComponentsQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListFleetComponentsQuery(),
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var component = Assert.Single(result.Get());
        Assert.False(component.IsHealthy);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var entity = await verifyContext
            .Set<FleetComponentEntity>()
            .SingleAsync(item => item.InstanceId == "api-stale");
        Assert.False(entity.IsHealthy);
    }

    private static ServiceProvider CreateProvider(
        SqliteConnection connection,
        FleetComponentOptions options
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StatusMapsterConfig).Assembly])
        );
        services.AddLogging();
        services.AddSingleton(options);
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new StatusMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(cfg =>
            cfg.WithQueryHandlersFrom(typeof(ListFleetComponentsQueryHandler).Assembly)
        );
        return services.BuildServiceProvider();
    }

    private static async Task EnsureCreatedAsync(AsyncServiceScope scope)
    {
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }
}
