using Mapster;
using MapsterMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class UserDeleteAccountQueryHandlerTests
{
    [Fact]
    public async Task RunQueryAsync_WhenPasswordInvalid_ReturnsNone()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Handler.RunQueryAsync(
            new UserDeleteAccountQuery
            {
                UserId = UserId.From(fixture.UserId),
                Password = "wrong-password",
            },
            CancellationToken.None
        );

        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task RunQueryAsync_WhenOwnedResourcesExist_ReturnsBlockers()
    {
        await using var fixture = await CreateFixtureAsync();

        await using (var context = await fixture.ContextFactory.CreateDbContextAsync())
        {
            context
                .Set<RepositoryEntity>()
                .Add(
                    new RepositoryEntity
                    {
                        Id = Guid.NewGuid(),
                        Name = "Blocked Repo",
                        Slug = "blocked-repo",
                        OwnerUserId = fixture.UserId,
                        PhysicalPath = $"./repositories/{fixture.UserId}/blocked-repo",
                    }
                );
            await context.SaveChangesAsync();
        }

        var result = await fixture.Handler.RunQueryAsync(
            new UserDeleteAccountQuery
            {
                UserId = UserId.From(fixture.UserId),
                Password = fixture.Password,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var payload = result.Get();
        Assert.False(payload.Success);
        Assert.Contains(payload.Blockers, blocker => blocker.Type == "repository");
    }

    [Fact]
    public async Task RunQueryAsync_WhenNoBlockers_DeletesAccount()
    {
        await using var fixture = await CreateFixtureAsync();

        var result = await fixture.Handler.RunQueryAsync(
            new UserDeleteAccountQuery
            {
                UserId = UserId.From(fixture.UserId),
                Password = fixture.Password,
            },
            CancellationToken.None
        );

        Assert.True(result.IsSome);
        var payload = result.Get();
        Assert.True(payload.Success);

        await using var context = await fixture.ContextFactory.CreateDbContextAsync();
        Assert.Null(await context.Set<UserEntity>().FindAsync(fixture.UserId));
    }

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(FeatureRegistration.GetFeatureAssemblies().ToArray())
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
            options.UseSqlite(connection).EnableServiceProviderCaching(false)
        );
        services.AddLogging();
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();

        var mapsterConfig = new TypeAdapterConfig();
        foreach (var assembly in FeatureRegistration.GetFeatureAssemblies())
        {
            var configType = assembly
                .GetTypes()
                .FirstOrDefault(type =>
                    type.Name.EndsWith("MapsterConfig", StringComparison.Ordinal)
                    && type.GetInterfaces().Any(i => i == typeof(IRegister))
                );
            if (configType is not null)
            {
                ((IRegister)Activator.CreateInstance(configType)!).Register(mapsterConfig);
            }
        }

        services.AddSingleton(mapsterConfig);
        services.AddSingleton<IMapper>(sp => new Mapper(sp.GetRequiredService<TypeAdapterConfig>()));
        services.AddSingleton<UserDeleteAccountQueryHandler>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        var userId = Guid.NewGuid();
        const string password = "DeleteMe123!";
        var hasher = provider.GetRequiredService<IPasswordHasherService>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context
                .Set<UserEntity>()
                .Add(
                    new UserEntity
                    {
                        Id = userId,
                        Username = "delete-me",
                        NormalizedUsername = "delete-me",
                        CreatedAt = DateTimeOffset.UtcNow,
                    }
                );
            context
                .Set<UserCredentialsEntity>()
                .Add(
                    new UserCredentialsEntity
                    {
                        UserId = userId,
                        PasswordHash = hasher.HashPassword(password),
                        Deleted = false,
                        SignInProvider = false,
                    }
                );
            await context.SaveChangesAsync();
        }

        return new Fixture(
            connection,
            provider,
            provider.GetRequiredService<UserDeleteAccountQueryHandler>(),
            contextFactory,
            userId,
            password
        );
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        public Fixture(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            UserDeleteAccountQueryHandler handler,
            IDbContextFactory<OpenGitBaseDbContext> contextFactory,
            Guid userId,
            string password
        )
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            Handler = handler;
            ContextFactory = contextFactory;
            UserId = userId;
            Password = password;
        }

        public UserDeleteAccountQueryHandler Handler { get; }

        public IDbContextFactory<OpenGitBaseDbContext> ContextFactory { get; }

        public Guid UserId { get; }

        public string Password { get; }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
