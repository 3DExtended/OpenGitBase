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
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class GetRepositoryBySlugForUserQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEntityMissing_ReturnsNone()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = BuildServiceProvider(connection);
        await using var scope = serviceProvider.CreateAsyncScope();
        await EnsureDatabaseAsync(scope.ServiceProvider);

        var handler =
            scope.ServiceProvider.GetRequiredService<GetRepositoryBySlugForUserQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryBySlugForUserQuery
            {
                Slug = "missing-repo",
                OwnerUserId = UserId.From(Guid.NewGuid()),
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenEntityExists_ReturnsMappedDto()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = BuildServiceProvider(connection);
        await using var scope = serviceProvider.CreateAsyncScope();
        var contextFactory = await EnsureDatabaseAsync(scope.ServiceProvider);

        var ownerUserId = UserId.From(Guid.NewGuid());
        var repositoryId = Guid.NewGuid();
        await SeedRepositoryAsync(
            contextFactory,
            repositoryId,
            ownerUserId,
            slug: "my-repo",
            name: "My Repository",
            isPrivate: true
        );

        var handler =
            scope.ServiceProvider.GetRequiredService<GetRepositoryBySlugForUserQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryBySlugForUserQuery { Slug = "my-repo", OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var dto = result.Get();
        Assert.Equal(RepositoryId.From(repositoryId), dto.Id);
        Assert.Equal("my-repo", dto.Slug);
        Assert.Equal("My Repository", dto.Name);
        Assert.Equal(ownerUserId, dto.OwnerUserId);
        Assert.True(dto.IsPrivate);
    }

    [Fact]
    public async Task RunQueryAsync_WhenSlugMatchesButDifferentOwner_ReturnsNone()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = BuildServiceProvider(connection);
        await using var scope = serviceProvider.CreateAsyncScope();
        var contextFactory = await EnsureDatabaseAsync(scope.ServiceProvider);

        var ownerUserId = UserId.From(Guid.NewGuid());
        var otherUserId = UserId.From(Guid.NewGuid());
        await SeedRepositoryAsync(
            contextFactory,
            Guid.NewGuid(),
            ownerUserId,
            slug: "shared-slug",
            name: "Owner repo"
        );

        var handler =
            scope.ServiceProvider.GetRequiredService<GetRepositoryBySlugForUserQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryBySlugForUserQuery { Slug = "shared-slug", OwnerUserId = otherUserId },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenOwnerMatchesButDifferentSlug_ReturnsNone()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = BuildServiceProvider(connection);
        await using var scope = serviceProvider.CreateAsyncScope();
        var contextFactory = await EnsureDatabaseAsync(scope.ServiceProvider);

        var ownerUserId = UserId.From(Guid.NewGuid());
        await SeedRepositoryAsync(
            contextFactory,
            Guid.NewGuid(),
            ownerUserId,
            slug: "existing-slug",
            name: "Existing repo"
        );

        var handler =
            scope.ServiceProvider.GetRequiredService<GetRepositoryBySlugForUserQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryBySlugForUserQuery { Slug = "other-slug", OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenMultipleRepositoriesExist_ReturnsMatchingOne()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = BuildServiceProvider(connection);
        await using var scope = serviceProvider.CreateAsyncScope();
        var contextFactory = await EnsureDatabaseAsync(scope.ServiceProvider);

        var ownerUserId = UserId.From(Guid.NewGuid());
        var otherUserId = UserId.From(Guid.NewGuid());
        var targetId = Guid.NewGuid();
        await SeedRepositoryAsync(
            contextFactory,
            Guid.NewGuid(),
            ownerUserId,
            slug: "first-repo",
            name: "First"
        );
        await SeedRepositoryAsync(
            contextFactory,
            targetId,
            ownerUserId,
            slug: "target-repo",
            name: "Target"
        );
        await SeedRepositoryAsync(
            contextFactory,
            Guid.NewGuid(),
            otherUserId,
            slug: "target-repo",
            name: "Other user copy"
        );

        var handler =
            scope.ServiceProvider.GetRequiredService<GetRepositoryBySlugForUserQueryHandler>();
        var result = await handler.RunQueryAsync(
            new GetRepositoryBySlugForUserQuery { Slug = "target-repo", OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Equal(RepositoryId.From(targetId), result.Get().Id);
        Assert.Equal("Target", result.Get().Name);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static ServiceProvider BuildServiceProvider(SqliteConnection connection)
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
            options.WithQueryHandlersFrom(typeof(GetRepositoryBySlugForUserQueryHandler).Assembly)
        );

        return services.BuildServiceProvider();
    }

    private static async Task<IDbContextFactory<OpenGitBaseDbContext>> EnsureDatabaseAsync(
        IServiceProvider serviceProvider
    )
    {
        var contextFactory = serviceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
        return contextFactory;
    }

    private static async Task SeedRepositoryAsync(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        Guid id,
        UserId ownerUserId,
        string slug,
        string name,
        bool isPrivate = false
    )
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        context
            .Set<RepositoryEntity>()
            .Add(
                new RepositoryEntity
                {
                    Id = id,
                    OwnerUserId = ownerUserId.Value,
                    Slug = slug,
                    Name = name,
                    PhysicalPath = $"./repositories/{ownerUserId.Value}/{slug}",
                    IsPrivate = isPrivate,
                }
            );
        await context.SaveChangesAsync();
    }
}
