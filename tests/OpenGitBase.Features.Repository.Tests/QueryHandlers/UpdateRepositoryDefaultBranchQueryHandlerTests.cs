using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class UpdateRepositoryDefaultBranchQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_RejectsUnknownBranchWhenBranchesExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var repositoryId = Guid.NewGuid();
        var services = BuildServices(connection);
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "Repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = "/srv/git/test.git",
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<
            UpdateRepositoryDefaultBranchQueryHandler
        >();
        var result = await handler.RunQueryAsync(
            new UpdateRepositoryDefaultBranchQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                DefaultBranchName = "missing",
                KnownBranchNames = ["main"],
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_UpdatesExistingBranch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var repositoryId = Guid.NewGuid();
        var services = BuildServices(connection);
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
            context.Set<RepositoryEntity>().Add(
                new RepositoryEntity
                {
                    Id = repositoryId,
                    Name = "Repo",
                    Slug = "repo",
                    OwnerUserId = Guid.NewGuid(),
                    PhysicalPath = "/srv/git/test.git",
                }
            );
            await context.SaveChangesAsync();
        }

        var handler = scope.ServiceProvider.GetRequiredService<
            UpdateRepositoryDefaultBranchQueryHandler
        >();
        var result = await handler.RunQueryAsync(
            new UpdateRepositoryDefaultBranchQuery
            {
                RepositoryId = RepositoryId.From(repositoryId),
                DefaultBranchName = "develop",
                KnownBranchNames = ["main", "develop"],
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal("develop", result.Get().DefaultBranchName);
    }

    private static ServiceCollection BuildServices(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider([typeof(RepositoryMapsterConfig).Assembly])
        );
        services.AddTestDbContextFactory<OpenGitBaseDbContext>(connection);
        services.AddLogging();
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(UpdateRepositoryDefaultBranchQueryHandler).Assembly)
        );
        return services;
    }
}
