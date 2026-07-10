using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class RepositoryKeyServiceTests
{
    [Fact]
    public async Task GenerateAndStoreKeyAsync_PersistsEnvelopeEncryptedKey()
    {
        var repositoryId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(context =>
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "demo",
                    Slug = "demo",
                    PhysicalPath = "/srv/git/demo.git",
                    OwnerUserId = Guid.NewGuid(),
                    ReplicationState = ReplicationState.Rf3Healthy,
                }
            );
        });
        await using (provider)
        {
            var version = await service.GenerateAndStoreKeyAsync(repositoryId, CancellationToken.None);

            Assert.Equal(1, version);

            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var stored = await context
                .Set<RepositoryKeyEntity>()
                .SingleAsync(key => key.RepositoryId == repositoryId);
            Assert.False(string.IsNullOrWhiteSpace(stored.KeyCiphertext));
        }
    }

    [Fact]
    public async Task TryGetEphemeralKeyForPrimaryAsync_WhenCallerIsPrimary_ReturnsKey()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(context =>
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "demo",
                    Slug = "demo",
                    PhysicalPath = "/srv/git/demo.git",
                    OwnerUserId = Guid.NewGuid(),
                    PrimaryStorageNodeId = primaryNodeId,
                    ReplicationState = ReplicationState.Rf3Healthy,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = primaryNodeId,
                            Role = RepositoryReplicaRole.Primary,
                        },
                    ],
                }
            );
        });

        await using (provider)
        {
            await service.GenerateAndStoreKeyAsync(repositoryId, CancellationToken.None);
            var key = await service.TryGetEphemeralKeyForPrimaryAsync(
                repositoryId,
                primaryNodeId,
                CancellationToken.None
            );

            Assert.NotNull(key);
            Assert.Equal(32, key!.KeyMaterial.Length);
        }
    }

    [Fact]
    public async Task TryGetEphemeralKeyForPrimaryAsync_WhenCallerIsNotPrimary_ReturnsNull()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var replicaNodeId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(context =>
        {
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "demo",
                    Slug = "demo",
                    PhysicalPath = "/srv/git/demo.git",
                    OwnerUserId = Guid.NewGuid(),
                    PrimaryStorageNodeId = primaryNodeId,
                    ReplicationState = ReplicationState.Rf3Healthy,
                    Replicas =
                    [
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = primaryNodeId,
                            Role = RepositoryReplicaRole.Primary,
                        },
                        new RepositoryReplicaEntity
                        {
                            RepositoryId = repositoryId,
                            StorageNodeId = replicaNodeId,
                            Role = RepositoryReplicaRole.Replica,
                        },
                    ],
                }
            );
        });

        await using (provider)
        {
            await service.GenerateAndStoreKeyAsync(repositoryId, CancellationToken.None);
            var key = await service.TryGetEphemeralKeyForPrimaryAsync(
                repositoryId,
                replicaNodeId,
                CancellationToken.None
            );

            Assert.Null(key);
        }
    }

    private static async Task<(RepositoryKeyService Service, ServiceProvider Provider)> CreateServiceAsync(
        Action<OpenGitBaseDbContext>? seed = null
    )
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<IRepositoryKeyProtectionService>(
            new RepositoryKeyProtectionService(
                new EmailProtectionService(
                    new EncryptionOptions
                    {
                        DataKey = Convert.ToBase64String(new byte[32]),
                        Pepper = "test-pepper",
                    }
                )
            )
        );
        services.AddSingleton<RepositoryKeyService>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            seed?.Invoke(context);
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<RepositoryKeyService>(), provider);
    }
}
