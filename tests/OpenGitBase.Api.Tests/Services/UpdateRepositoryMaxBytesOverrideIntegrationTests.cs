using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class UpdateRepositoryMaxBytesOverrideIntegrationTests
{
    [Fact]
    public async Task RunQueryAsync_WhenFullyEligible_PersistsOverrideWithoutMockedService()
    {
        var orgId = Guid.NewGuid();
        var repositoryId = Guid.NewGuid();
        var (handler, provider) = await CreateIntegrationHandlerAsync(orgId, repositoryId);

        await using (provider)
        {
            var result = await handler.RunQueryAsync(
                new UpdateRepositoryMaxBytesOverrideQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    MaxBytesOverride = 2_000_000_000,
                },
                CancellationToken.None
            );

            Assert.True(result.IsSome);
            Assert.Equal(2_000_000_000, result.Get().MaxBytesOverride);

            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var stored = await context
                .Set<RepositoryEntity>()
                .SingleAsync(repository => repository.Id == repositoryId);
            Assert.Equal(2_000_000_000, stored.MaxBytesOverride);
        }
    }

    private static async Task<(UpdateRepositoryMaxBytesOverrideQueryHandler Handler, ServiceProvider Provider)>
        CreateIntegrationHandlerAsync(Guid orgId, Guid repositoryId)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(
                [typeof(RepositoryMapsterConfig).Assembly, typeof(StorageNodeEntity).Assembly]
            )
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddSingleton(new StorageNodeOptions { MissedHeartbeatThresholdSeconds = 300 });
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(Arg.Any<GetOrganizationQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = OrganizationId.From(orgId),
                        Name = "Org",
                        Slug = "org",
                        OwnerUserId = Guid.NewGuid(),
                    }
                )
            );
        services.AddSingleton(queryProcessor);
        services.AddSingleton<RepositoryByteOverrideService>();
        services.AddSingleton<IRepositoryByteOverrideService>(sp =>
            sp.GetRequiredService<RepositoryByteOverrideService>()
        );
        services.AddSingleton<UpdateRepositoryMaxBytesOverrideQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            var orgNodes = Enumerable
                .Range(1, 4)
                .Select(index => new StorageNodeEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = $"org-node-{index}",
                    InternalHost = $"org-node-{index}",
                    InternalHttpPort = 8081,
                    OwnerOrganizationId = orgId,
                    MaxBytes = 1_000_000_000,
                    IsHealthy = true,
                    LastHeartbeatAt = DateTimeOffset.UtcNow,
                    RegisteredAt = DateTimeOffset.UtcNow,
                })
                .ToList();
            context.Set<StorageNodeEntity>().AddRange(orgNodes);
            context
                .Set<RepositoryEntity>()
                .Add(
                    new RepositoryEntity
                    {
                        Id = repositoryId,
                        Name = "Repo",
                        Slug = "repo",
                        OwnerUserId = orgId,
                        PhysicalPath = "/srv/git/repo.git",
                        Replicas =
                        [
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = orgNodes[0].Id,
                                Role = RepositoryReplicaRole.Primary,
                            },
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = orgNodes[1].Id,
                                Role = RepositoryReplicaRole.ReadReplica,
                            },
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = orgNodes[2].Id,
                                Role = RepositoryReplicaRole.EncryptedReplica,
                            },
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = orgNodes[3].Id,
                                Role = RepositoryReplicaRole.EncryptedReplica,
                            },
                        ],
                    }
                );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<UpdateRepositoryMaxBytesOverrideQueryHandler>(), provider);
    }
}
