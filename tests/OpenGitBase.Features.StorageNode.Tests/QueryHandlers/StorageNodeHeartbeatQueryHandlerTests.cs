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

public class StorageNodeHeartbeatQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_UpdatesDiskStatsAndMarksHealthy()
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
            var entity = StorageNodeTestData.CreateEntity(isHealthy: false, freeBytes: 100);
            context.Set<Entities.StorageNodeEntity>().Add(entity);
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<StorageNodeHeartbeatQueryHandler>();
        var result = await handler.RunQueryAsync(
            new StorageNodeHeartbeatQuery
            {
                NodeId = StorageNodeTestData.SampleNodeId,
                FreeBytesAvailable = 9_000_000,
                TotalBytesAvailable = 10_000_000,
                CertificateThumbprint = StorageNodeTestData.SampleCertificateThumbprint,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.True(result.Get().Acknowledged);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var updated = await verifyContext
            .Set<Entities.StorageNodeEntity>()
            .SingleAsync(node => node.NodeId == StorageNodeTestData.SampleNodeId);
        Assert.True(updated.IsHealthy);
        Assert.Equal(9_000_000, updated.FreeBytesAvailable);
    }

    private static ServiceCollection CreateServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        services.AddSingleton(new StorageNodeOptions());
        var mapsterConfig = new TypeAdapterConfig();
        new StorageNodeMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(StorageNodeHeartbeatQueryHandler).Assembly)
        );
        return services;
    }
}
