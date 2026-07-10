using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class UpdateRepositoryMaxBytesOverrideQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEligible_SetsOverride()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, contextFactory, byteOverrideService) = await CreateHandlerAsync(repositoryId);

        byteOverrideService
            .EvaluateAsync(Arg.Any<RepositoryEntity>(), Arg.Any<CancellationToken>())
            .Returns(
                new RepositoryByteOverrideEligibilityDto
                {
                    Eligible = true,
                    MaxAllowedOverride = 10_000_000_000,
                }
            );

        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                MaxBytesOverride = 5_000_000_000,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(5_000_000_000, result.Get().MaxBytesOverride);

        await using var context = await contextFactory.CreateDbContextAsync();
        var stored = await context
            .Set<RepositoryEntity>()
            .SingleAsync(repository => repository.Id == repositoryId);
        Assert.Equal(5_000_000_000, stored.MaxBytesOverride);
    }

    [Fact]
    public async Task RunQueryAsync_WhenClearingOverride_PersistsNull()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, contextFactory, _) = await CreateHandlerAsync(
            repositoryId,
            existingOverride: 2_000_000_000
        );

        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                MaxBytesOverride = null,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Null(result.Get().MaxBytesOverride);

        await using var context = await contextFactory.CreateDbContextAsync();
        var stored = await context
            .Set<RepositoryEntity>()
            .SingleAsync(repository => repository.Id == repositoryId);
        Assert.Null(stored.MaxBytesOverride);
    }

    [Fact]
    public async Task RunQueryAsync_WhenNotEligible_ReturnsNone()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, _, byteOverrideService) = await CreateHandlerAsync(repositoryId);

        byteOverrideService
            .EvaluateAsync(Arg.Any<RepositoryEntity>(), Arg.Any<CancellationToken>())
            .Returns(
                new RepositoryByteOverrideEligibilityDto
                {
                    Eligible = false,
                    Reason = "Organization must operate more than three healthy storage nodes.",
                }
            );

        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                MaxBytesOverride = 1_000_000,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenOverrideExceedsMaxAllowed_ReturnsNone()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, _, byteOverrideService) = await CreateHandlerAsync(repositoryId);

        byteOverrideService
            .EvaluateAsync(Arg.Any<RepositoryEntity>(), Arg.Any<CancellationToken>())
            .Returns(
                new RepositoryByteOverrideEligibilityDto
                {
                    Eligible = true,
                    MaxAllowedOverride = 1_000_000,
                }
            );

        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                MaxBytesOverride = 2_000_000,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenRepositoryMissing_ReturnsNone()
    {
        var (handler, _, _) = await CreateHandlerAsync(Guid.NewGuid());

        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(Guid.NewGuid()),
                MaxBytesOverride = 1_000,
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenOverrideZeroOrNegative_ClearsOverride()
    {
        var repositoryId = Guid.NewGuid();
        var (handler, contextFactory, _) = await CreateHandlerAsync(
            repositoryId,
            existingOverride: 2_000_000_000
        );

        var result = await handler.RunQueryAsync(
            new UpdateRepositoryMaxBytesOverrideQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                MaxBytesOverride = 0,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Null(result.Get().MaxBytesOverride);

        await using var context = await contextFactory.CreateDbContextAsync();
        var stored = await context
            .Set<RepositoryEntity>()
            .SingleAsync(repository => repository.Id == repositoryId);
        Assert.Null(stored.MaxBytesOverride);
    }

    private static async Task<(
        UpdateRepositoryMaxBytesOverrideQueryHandler Handler,
        IDbContextFactory<OpenGitBaseDbContext> ContextFactory,
        IRepositoryByteOverrideService ByteOverrideService
    )> CreateHandlerAsync(Guid repositoryId, long? existingOverride = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));

        var byteOverrideService = Substitute.For<IRepositoryByteOverrideService>();
        services.AddSingleton(byteOverrideService);
        services.AddSingleton<UpdateRepositoryMaxBytesOverrideQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context
                .Set<RepositoryEntity>()
                .Add(
                    new RepositoryEntity
                    {
                        Id = repositoryId,
                        Name = "Repo",
                        Slug = "repo",
                        OwnerUserId = Guid.NewGuid(),
                        PhysicalPath = "/srv/git/repo.git",
                        MaxBytesOverride = existingOverride,
                        Replicas = [],
                    }
                );
            await context.SaveChangesAsync();
        }

        return (
            provider.GetRequiredService<UpdateRepositoryMaxBytesOverrideQueryHandler>(),
            contextFactory,
            byteOverrideService
        );
    }
}
