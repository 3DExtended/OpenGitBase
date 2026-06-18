using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;
using OpenGitBase.Features.StorageNode.QueryHandlers;

namespace OpenGitBase.Features.StorageNode.Tests.QueryHandlers;

public class GetStorageNodeApiTokenQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_ReturnsDecryptedToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var emailProtection = new EmailProtectionService(
            new EncryptionOptions
            {
                DataKey = Convert.ToBase64String(new byte[32]),
                Pepper = "test-pepper",
            }
        );
        var plainToken = "AbC+/SecretToken==";
        var storageNodeId = Guid.NewGuid();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(StorageNodeMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        services.AddSingleton<IEmailProtectionService>(emailProtection);
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(GetStorageNodeApiTokenQueryHandler).Assembly)
        );

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context
                .Set<StorageNodeEntity>()
                .Add(
                    new StorageNodeEntity
                    {
                        Id = storageNodeId,
                        NodeId = "storage-1",
                        InternalHost = "storage-1",
                        InternalHttpPort = 8081,
                        ApiTokenHash = "hash",
                        ApiTokenProtected = emailProtection.EncryptSecret(plainToken),
                        IsHealthy = true,
                        RegisteredAt = DateTimeOffset.UtcNow,
                    }
                );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<GetStorageNodeApiTokenQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetStorageNodeApiTokenQuery
            {
                StorageNodeId = StorageNodeId.From(storageNodeId),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(plainToken, result.Get());
    }
}
