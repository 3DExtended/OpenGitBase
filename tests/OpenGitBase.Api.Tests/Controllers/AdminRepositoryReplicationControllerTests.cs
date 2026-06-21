﻿using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Controllers;
using OpenGitBase.Api.Models;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Controllers;

public class AdminRepositoryReplicationControllerTests
{
    [Fact]
    public async Task GetRepositoryStatus_WhenRepositoryMissing_ReturnsNotFound()
    {
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var result = await controller.GetRepositoryStatus(
                Guid.NewGuid(),
                CancellationToken.None
            );

            Assert.IsType<NotFoundResult>(result.Result);
        }
    }

    [Fact]
    public async Task GetRepositoryStatus_WhenRepositoryExists_ReturnsReplicationDetail()
    {
        var repositoryId = Guid.NewGuid();
        var primaryNodeId = Guid.NewGuid();
        var (controller, provider) = await CreateControllerAsync();
        await using (provider)
        {
            var contextFactory = provider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using (var context = await contextFactory.CreateDbContextAsync())
            {
                context.Set<RepositoryEntity>().Add(
                    new RepositoryEntity
                    {
                        Id = repositoryId,
                        Name = "repo",
                        Slug = "repo",
                        OwnerUserId = Guid.NewGuid(),
                        PhysicalPath = $"/srv/git/{repositoryId}.git",
                        StorageNodeId = primaryNodeId,
                        PrimaryStorageNodeId = primaryNodeId,
                        PrimaryWatermark = 2,
                        ReplicationEpoch = 1,
                        ReplicationState = ReplicationState.Rf3Healthy,
                        Replicas =
                        [
                            new RepositoryReplicaEntity
                            {
                                RepositoryId = repositoryId,
                                StorageNodeId = primaryNodeId,
                                Role = RepositoryReplicaRole.Primary,
                                AppliedWatermark = 2,
                            },
                        ],
                    }
                );
                await context.SaveChangesAsync();
            }

            var result = await controller.GetRepositoryStatus(
                repositoryId,
                CancellationToken.None
            );

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var body = Assert.IsType<AdminRepositoryReplicationStatusResponse>(ok.Value);
            Assert.Equal("Rf3Healthy", body.ReplicationState);
            Assert.Single(body.Replicas);
        }
    }

    private static async Task<(
        AdminRepositoryReplicationController Controller,
        ServiceProvider Provider
    )> CreateControllerAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var queryProcessor = Substitute.For<IQueryProcessor>();
        queryProcessor
            .RunQueryAsync(
                Arg.Any<RepositoryReplicationRoutingQuery>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Option.From(
                    new RepositoryReplicationRoutingDto { WriteQuorumAvailable = true }
                )
            );

        var services = new ServiceCollection();
        services.AddSingleton(connection);
        services.AddSingleton(queryProcessor);
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var controller = new AdminRepositoryReplicationController(contextFactory, queryProcessor);
        return (controller, provider);
    }
}
