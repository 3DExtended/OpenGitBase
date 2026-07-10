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
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class RepositoryByteOverrideServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WhenFullyOrgHostedWithFourNodes_IsEligible()
    {
        var orgId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(orgId, orgNodeCount: 4, allOrgOwned: true);

        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();

            var result = await service.EvaluateAsync(repository, CancellationToken.None);

            Assert.True(result.Eligible);
            Assert.Equal(4, result.OrgContributedNodeCount);
            Assert.Equal(4_000_000_000, result.MaxAllowedOverride);
        }
    }

    [Fact]
    public async Task EvaluateAsync_WhenOrgHasOnlyThreeNodes_IsNotEligible()
    {
        var orgId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(orgId, orgNodeCount: 3, allOrgOwned: true);

        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();

            var result = await service.EvaluateAsync(repository, CancellationToken.None);

            Assert.False(result.Eligible);
            Assert.Contains("more than three", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task EvaluateAsync_WhenPlatformReplicaPresent_IsNotEligible()
    {
        var orgId = Guid.NewGuid();
        var (service, provider) = await CreateServiceAsync(orgId, orgNodeCount: 4, allOrgOwned: false);

        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .SingleAsync();

            var result = await service.EvaluateAsync(repository, CancellationToken.None);

            Assert.False(result.Eligible);
            Assert.Contains("organization-owned", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static async Task<(RepositoryByteOverrideService Service, ServiceProvider Provider)> CreateServiceAsync(
        Guid orgId,
        int orgNodeCount,
        bool allOrgOwned
    )
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

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            var orgNodes = Enumerable
                .Range(1, orgNodeCount)
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

            StorageNodeEntity? platformNode = null;
            if (!allOrgOwned)
            {
                platformNode = new StorageNodeEntity
                {
                    Id = Guid.NewGuid(),
                    NodeId = "platform-node",
                    InternalHost = "platform-node",
                    InternalHttpPort = 8081,
                    IsHealthy = true,
                    LastHeartbeatAt = DateTimeOffset.UtcNow,
                    RegisteredAt = DateTimeOffset.UtcNow,
                };
                context.Set<StorageNodeEntity>().Add(platformNode);
            }

            context.Set<StorageNodeEntity>().AddRange(orgNodes);
            var repositoryId = Guid.NewGuid();
            var replicas = new List<RepositoryReplicaEntity>
            {
                new()
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = orgNodes[0].Id,
                    Role = RepositoryReplicaRole.Primary,
                },
            };

            if (orgNodeCount >= 4)
            {
                replicas.Add(
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = orgNodes[1].Id,
                        Role = RepositoryReplicaRole.ReadReplica,
                    }
                );
                replicas.Add(
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = orgNodes[2].Id,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                    }
                );
                replicas.Add(
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = allOrgOwned ? orgNodes[3].Id : platformNode!.Id,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                    }
                );
            }
            else
            {
                replicas.Add(
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = orgNodes[Math.Min(1, orgNodeCount - 1)].Id,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                    }
                );
                replicas.Add(
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repositoryId,
                        StorageNodeId = allOrgOwned
                            ? orgNodes[Math.Min(2, orgNodeCount - 1)].Id
                            : platformNode!.Id,
                        Role = RepositoryReplicaRole.EncryptedReplica,
                    }
                );
            }

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
                        Replicas = replicas,
                    }
                );
            await context.SaveChangesAsync();
        }

        return (provider.GetRequiredService<RepositoryByteOverrideService>(), provider);
    }
}
