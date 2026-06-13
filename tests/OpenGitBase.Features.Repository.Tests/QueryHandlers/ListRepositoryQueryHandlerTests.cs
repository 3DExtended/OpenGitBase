﻿using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Repository.QueryHandlers;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Repository.Tests.QueryHandlers;

public class ListRepositoryQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenEmpty_ReturnsEmptyList()
    {
        await using var connection = await OpenConnectionAsync();
        await using var serviceProvider = BuildServiceProvider(connection);
        await using var scope = serviceProvider.CreateAsyncScope();
        await EnsureDatabaseAsync(scope.ServiceProvider);

        var ownerUserId = UserId.From(Guid.NewGuid());
        var handler = scope.ServiceProvider.GetRequiredService<ListRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryQuery { OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Empty(result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_ReturnsOnlyRepositoriesForRequestedUser()
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
            slug: "owner-repo",
            name: "Owner repo"
        );
        await SeedRepositoryAsync(
            contextFactory,
            Guid.NewGuid(),
            otherUserId,
            slug: "other-repo",
            name: "Other repo"
        );

        var handler = scope.ServiceProvider.GetRequiredService<ListRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryQuery { OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var repositories = result.Get();
        Assert.Single(repositories);
        Assert.Equal("owner-repo", repositories[0].Slug);
        Assert.Equal(ownerUserId, repositories[0].OwnerUserId);
    }

    [Fact]
    public async Task RunQueryAsync_WhenOtherUserHasRepositories_ReturnsEmptyForUserWithNone()
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
            otherUserId,
            slug: "other-repo",
            name: "Other repo"
        );

        var handler = scope.ServiceProvider.GetRequiredService<ListRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryQuery { OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        Assert.Empty(result.Get());
    }

    [Fact]
    public async Task RunQueryAsync_ReturnsMultipleRepositoriesForSameUser()
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
            slug: "first-repo",
            name: "First"
        );
        await SeedRepositoryAsync(
            contextFactory,
            Guid.NewGuid(),
            ownerUserId,
            slug: "second-repo",
            name: "Second"
        );

        var handler = scope.ServiceProvider.GetRequiredService<ListRepositoryQueryHandler>();
        var result = await handler.RunQueryAsync(
            new ListRepositoryQuery { OwnerUserId = ownerUserId },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var slugs = result
            .Get()
            .Select(repository => repository.Slug)
            .OrderBy(slug => slug)
            .ToList();
        Assert.Equal(["first-repo", "second-repo"], slugs);
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
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection)
        );
        services.AddLogging();
        var mapsterConfig = new TypeAdapterConfig();
        new RepositoryMapsterConfig().Register(mapsterConfig);
        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(
            sp.GetRequiredService<TypeAdapterConfig>()
        ));
        services.AddCqrs(options =>
            options.WithQueryHandlersFrom(typeof(ListRepositoryQueryHandler).Assembly)
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
        await SeedUserIfMissingAsync(context, ownerUserId.Value);
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

    private static async Task SeedUserIfMissingAsync(OpenGitBaseDbContext context, Guid userId)
    {
        if (await context.Set<UserEntity>().AnyAsync(entity => entity.Id == userId))
        {
            return;
        }

        context
            .Set<UserEntity>()
            .Add(
                new UserEntity
                {
                    Id = userId,
                    Username = $"user-{userId:N}",
                    NormalizedUsername = $"USER-{userId:N}".ToUpperInvariant(),
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        await context.SaveChangesAsync();
    }
}
