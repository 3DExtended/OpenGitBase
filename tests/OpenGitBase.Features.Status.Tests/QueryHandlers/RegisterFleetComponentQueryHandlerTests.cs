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
using OpenGitBase.Features.Status.QueryHandlers;

namespace OpenGitBase.Features.Status.Tests.QueryHandlers;

public class RegisterFleetComponentQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_CreatesComponentAndReturnsHeartbeatInterval()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<RegisterFleetComponentQueryHandler>();
        var result = await handler.RunQueryAsync(
            new RegisterFleetComponentQuery
            {
                ComponentType = FleetComponentType.Api,
                InstanceId = "api-1",
                ProbeUrl = "http://api-1:8080/health",
                Version = "test",
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(30, result.Get().HeartbeatIntervalSeconds);

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context
            .Set<Entities.FleetComponentEntity>()
            .SingleAsync(component => component.InstanceId == "api-1");
        Assert.Equal(FleetComponentType.Api, entity.ComponentType);
        Assert.True(entity.IsHealthy);
        Assert.NotNull(entity.LastHeartbeatAt);
    }

    [Fact]
    public async Task RunQueryAsync_NormalizesLocalhostProbeUrlToDockerDns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<RegisterFleetComponentQueryHandler>();
        await handler.RunQueryAsync(
            new RegisterFleetComponentQuery
            {
                ComponentType = FleetComponentType.Git,
                InstanceId = "dispatcher-1",
                ProbeUrl = "http://127.0.0.1:8082/health",
            },
            CancellationToken.None
        );

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        var entity = await context
            .Set<Entities.FleetComponentEntity>()
            .SingleAsync(component => component.InstanceId == "dispatcher-1");
        Assert.Equal("http://dispatcher-1:8082/health", entity.ProbeUrl);
    }

    [Fact]
    public async Task RunQueryAsync_ReRegistrationUpdatesProbeUrl()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var provider = CreateProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        await EnsureCreatedAsync(scope);

        var handler = scope.ServiceProvider.GetRequiredService<RegisterFleetComponentQueryHandler>();
        await handler.RunQueryAsync(
            new RegisterFleetComponentQuery
            {
                ComponentType = FleetComponentType.Api,
                InstanceId = "api-1",
                ProbeUrl = "http://api-1:8080/health",
            },
            CancellationToken.None
        );

        await handler.RunQueryAsync(
            new RegisterFleetComponentQuery
            {
                ComponentType = FleetComponentType.Api,
                InstanceId = "api-1",
                ProbeUrl = "http://api-1:8080/health?details=false",
            },
            CancellationToken.None
        );

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        var count = await context.Set<Entities.FleetComponentEntity>().CountAsync();
        var entity = await context
            .Set<Entities.FleetComponentEntity>()
            .SingleAsync(component => component.InstanceId == "api-1");
        Assert.Equal(1, count);
        Assert.Equal("http://api-1:8080/health?details=false", entity.ProbeUrl);
    }

    private static ServiceProvider CreateProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StatusMapsterConfig).Assembly])
        );
        services.AddLogging();
        services.AddSingleton(new FleetComponentOptions());
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new StatusMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(RegisterFleetComponentQueryHandler).Assembly)
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
