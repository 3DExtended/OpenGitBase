using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class UpdateStorageNodeCapacityQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEnforceUsedBytesFloorAndBelowUsed_ReturnsNone()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = CreateServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        StorageNodeEntity entity;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            entity = new StorageNodeEntity
            {
                Id = Guid.NewGuid(),
                NodeId = "org-node-1",
                InternalHost = "org-node-1",
                InternalHttpPort = 8081,
                ApiTokenHash = "hash",
                MaxBytes = 1_000_000,
                UsedBytes = 500_000,
                IsHealthy = true,
                RegisteredAt = DateTimeOffset.UtcNow,
                CertificateThumbprint = "ABC",
            };
            context.Set<StorageNodeEntity>().Add(entity);
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<UpdateStorageNodeCapacityQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateStorageNodeCapacityQuery
            {
                StorageNodeId = StorageNodeId.From(entity.Id),
                MaxBytes = 400_000,
                EnforceUsedBytesFloor = true,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEnforceUsedBytesFloorAndAboveUsed_UpdatesMaxBytes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = CreateServices(connection);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        StorageNodeEntity entity;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            entity = new StorageNodeEntity
            {
                Id = Guid.NewGuid(),
                NodeId = "org-node-1",
                InternalHost = "org-node-1",
                InternalHttpPort = 8081,
                ApiTokenHash = "hash",
                MaxBytes = 1_000_000,
                UsedBytes = 500_000,
                IsHealthy = true,
                RegisteredAt = DateTimeOffset.UtcNow,
                CertificateThumbprint = "ABC",
            };
            context.Set<StorageNodeEntity>().Add(entity);
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<UpdateStorageNodeCapacityQueryHandler>();
        var result = await handler.RunQueryAsync(
            new UpdateStorageNodeCapacityQuery
            {
                StorageNodeId = StorageNodeId.From(entity.Id),
                MaxBytes = 2_000_000,
                EnforceUsedBytesFloor = true,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(2_000_000, result.Get().MaxBytes);
    }

    private static ServiceCollection CreateServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        var mapsterConfig = new TypeAdapterConfig();
        new StorageNodeMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(UpdateStorageNodeCapacityQueryHandler).Assembly)
        );
        return services;
    }
}
