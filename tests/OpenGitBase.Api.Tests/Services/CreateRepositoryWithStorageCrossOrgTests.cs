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
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Tests.Services;

public class CreateRepositoryWithStorageCrossOrgTests
{
    [Fact]
    public async Task RunQueryAsync_OrgRepo_PersistsCrossOrgEncryptedReplicas()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var orgNode = CreateNode("org-a-node", 500, orgA);
        var crossOrg = CreateNode("org-b-node", 450, orgB, HostingScope.CrossOrgAllowed);
        var platformA = CreateNode("storage-1", 900, null);
        var platformB = CreateNode("storage-2", 800, null);
        var platformC = CreateNode("storage-3", 700, null);
        var nodes = new[] { orgNode, crossOrg, platformA, platformB, platformC };

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetOrganizationQuery>(query => query.ModelId.Value == orgA),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new OrganizationDto
                    {
                        Id = OrganizationId.From(orgA),
                        Name = "Org A",
                        Slug = "org-a",
                        OwnerUserId = Guid.NewGuid(),
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Is<GetOrganizationStorageSettingsQuery>(query =>
                    query.OrganizationId.Value == orgA
                ),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new OrganizationStorageSettingsDto
                    {
                        OrganizationId = orgA,
                        DefaultSelfHostPreference = SelfHostPreference.PreferSelfHost,
                        DefaultPlacementPolicy = PlacementPolicy.MaxSelfHost,
                    }
                )
            );
        queryProcessor
            .RunQueryAsync(
                Arg.Any<ListHealthyStorageNodesQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Option.From<IReadOnlyList<StorageNodeDto>>(nodes));

        foreach (var node in nodes)
        {
            queryProcessor
                .RunQueryAsync(
                    Arg.Is<GetStorageNodeApiTokenQuery>(query =>
                        query.StorageNodeId == node.Id
                    ),
                    Arg.Any<CancellationToken>()
                )
                .Returns(Option.From($"token-{node.NodeId}"));
        }

        var provisioner = Substitute.For<IStorageProvisionerClient>();
        provisioner
            .ProvisionRepositoryAsync(
                Arg.Any<StorageNodeDto>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<long>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(new StorageProvisionerResult { Success = true });

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddSingleton(queryProcessor);
        services.AddSingleton(provisioner);
        services.AddSingleton<IRepositoryKeyService>(Substitute.For<IRepositoryKeyService>());
        services.AddSingleton(new RepositoryStorageQuotaOptions { Enabled = false });
        services.AddSingleton<CreateRepositoryWithStorageQueryHandler>();

        await using var serviceProvider = services.BuildServiceProvider();
        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var handler = serviceProvider.GetRequiredService<CreateRepositoryWithStorageQueryHandler>();
        var result = await handler.RunQueryAsync(
            new CreateRepositoryWithStorageQuery
            {
                ModelToCreate = new RepositoryDto
                {
                    Name = "Cross Org",
                    OwnerUserId = UserId.From(orgA),
                    Slug = "cross-org",
                    PhysicalPath = string.Empty,
                },
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.NotNull(result.Get().RepositoryId);

        await using var verifyContext = await contextFactory.CreateDbContextAsync();
        var repository = await verifyContext
            .Set<RepositoryEntity>()
            .Include(entity => entity.Replicas)
            .SingleAsync();

        var encryptedNodeIds = repository
            .Replicas.Where(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            .Select(replica => replica.StorageNodeId)
            .ToList();

        Assert.Contains(crossOrg.Id.Value, encryptedNodeIds);
    }

    private static StorageNodeDto CreateNode(
        string nodeId,
        long freeBytes,
        Guid? ownerOrganizationId,
        HostingScope hostingScope = HostingScope.OwnOrgOnly
    ) =>
        new()
        {
            Id = StorageNodeId.From(Guid.NewGuid()),
            NodeId = nodeId,
            InternalHost = nodeId,
            InternalHttpPort = 8081,
            FreeBytesAvailable = freeBytes,
            OwnerOrganizationId = ownerOrganizationId,
            HostingScope = hostingScope,
            IsHealthy = true,
        };
}
