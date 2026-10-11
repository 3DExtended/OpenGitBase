using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class GetRepositoryReplicationContextQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_TellsTheHookThatTheApiCreatesArtifacts()
    {
        var repositoryId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        await using var provider = await CreateProviderAsync(repositoryId, primaryId);
        var handler = provider.GetRequiredService<GetRepositoryReplicationContextQueryHandler>();

        var result = await handler.RunQueryAsync(
            new GetRepositoryReplicationContextQuery
            {
                RepositoryId = global::OpenGitBase.Features.Repository.Contracts.RepositoryId.From(repositoryId),
                StorageNodeId = StorageNodeId.From(primaryId),
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var context = result.Get();
        Assert.True(context.IsPrimary);
        Assert.Equal(5, context.PrimaryWatermark);
        Assert.True(context.ApiCreatesArtifacts);
    }

    private static async Task<ServiceProvider> CreateProviderAsync(Guid repositoryId, Guid primaryId)
    {
        var connection = SqliteTestConnection.OpenInMemory();
        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [
                    typeof(RepositoryMapsterConfig).Assembly,
                    typeof(global::OpenGitBase.Features.StorageNode.StorageNodeMapsterConfig).Assembly,
                ]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton<GetRepositoryReplicationContextQueryHandler>();
        var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        context.Set<RepositoryEntity>().Add(
            new RepositoryEntity
            {
                Id = repositoryId,
                Name = "repo",
                Slug = "repo",
                OwnerUserId = Guid.NewGuid(),
                PhysicalPath = $"/srv/git/{repositoryId}.git",
                StorageNodeId = primaryId,
                PrimaryStorageNodeId = primaryId,
                ReadReplicaStorageNodeId = primaryId,
                PrimaryWatermark = 5,
                ReplicationEpoch = 1,
                ReplicationState = ReplicationState.Rf4Healthy,
                Replicas =
                [
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = primaryId,
                        Role = RepositoryReplicaRole.Primary,
                        AppliedWatermark = 5,
                    },
                ],
            }
        );
        await context.SaveChangesAsync();
        return provider;
    }
}
