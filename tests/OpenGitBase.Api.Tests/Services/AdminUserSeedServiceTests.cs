using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenGitBase.Api;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class AdminUserSeedServiceTests
{
    [Fact]
    public async Task StartAsync_WhenEnabled_CreatesAdminUserAndOrganization()
    {
        await using var fixture = await CreateFixtureAsync(new AdminSeedOptions());

        await fixture.Service.StartAsync(CancellationToken.None);

        await using var context = await fixture.ContextFactory.CreateDbContextAsync();
        var user = await context
            .Set<UserEntity>()
            .SingleAsync(user => user.NormalizedUsername == "ADMIN");
        Assert.True(user.IsAdmin);

        var organization = await context
            .Set<OrganizationEntity>()
            .SingleAsync(org => org.Slug == "opengitbase");
        Assert.Equal("OpenGitBase", organization.Name);
        Assert.Equal(user.Id, organization.OwnerUserId);

        var member = await context
            .Set<OrganizationMemberEntity>()
            .SingleAsync(member => member.OrganizationId == organization.Id);
        Assert.Equal(user.Id, member.UserId);
        Assert.Equal(OrganizationMemberRole.Owner, member.Role);
    }

    [Fact]
    public async Task StartAsync_WhenAdminAlreadyExists_CreatesOrganization()
    {
        await using var fixture = await CreateFixtureAsync(new AdminSeedOptions());
        var existingUserId = Guid.NewGuid();
        await using (var context = await fixture.ContextFactory.CreateDbContextAsync())
        {
            context
                .Set<UserEntity>()
                .Add(
                    new UserEntity
                    {
                        Id = existingUserId,
                        Username = "admin",
                        NormalizedUsername = "ADMIN",
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsAdmin = true,
                    }
                );
            await context.SaveChangesAsync();
        }

        await fixture.Service.StartAsync(CancellationToken.None);

        await using var verifyContext = await fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(
            1,
            await verifyContext.Set<UserEntity>().CountAsync(user => user.NormalizedUsername == "ADMIN")
        );
        Assert.Equal(
            1,
            await verifyContext.Set<OrganizationEntity>().CountAsync(org => org.Slug == "opengitbase")
        );
    }

    [Fact]
    public async Task StartAsync_WhenOrganizationAlreadyExists_DoesNotDuplicate()
    {
        await using var fixture = await CreateFixtureAsync(new AdminSeedOptions());
        var existingUserId = Guid.NewGuid();
        var existingOrganizationId = Guid.NewGuid();
        await using (var context = await fixture.ContextFactory.CreateDbContextAsync())
        {
            context
                .Set<UserEntity>()
                .Add(
                    new UserEntity
                    {
                        Id = existingUserId,
                        Username = "admin",
                        NormalizedUsername = "ADMIN",
                        CreatedAt = DateTimeOffset.UtcNow,
                        IsAdmin = true,
                    }
                );
            context
                .Set<OrganizationEntity>()
                .Add(
                    new OrganizationEntity
                    {
                        Id = existingOrganizationId,
                        Name = "OpenGitBase",
                        Slug = "opengitbase",
                        OwnerUserId = existingUserId,
                    }
                );
            await context.SaveChangesAsync();
        }

        await fixture.Service.StartAsync(CancellationToken.None);

        await using var verifyContext = await fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(1, await verifyContext.Set<OrganizationEntity>().CountAsync());
        Assert.Equal(existingOrganizationId, (await verifyContext.Set<OrganizationEntity>().SingleAsync()).Id);
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_DoesNothing()
    {
        await using var fixture = await CreateFixtureAsync(
            new AdminSeedOptions { Enabled = false }
        );

        await fixture.Service.StartAsync(CancellationToken.None);

        await using var context = await fixture.ContextFactory.CreateDbContextAsync();
        Assert.Equal(0, await context.Set<UserEntity>().CountAsync());
        Assert.Equal(0, await context.Set<OrganizationEntity>().CountAsync());
    }

    private static async Task<Fixture> CreateFixtureAsync(AdminSeedOptions options)
    {
        var connection = SqliteTestConnection.OpenInMemory();

        var services = new ServiceCollection();
        services.AddSingleton<IFeatureAssemblyProvider>(
            new FeatureAssemblyProvider(FeatureRegistration.GetFeatureAssemblies().ToArray())
        );
        services.AddDbContextFactory<OpenGitBaseDbContext>(contextOptions =>
            contextOptions.UseSqlite(connection).EnableServiceProviderCaching(false)
        );
        services.AddLogging();
        services.AddSingleton(
            new EncryptionOptions
            {
                DataKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                Pepper = "dev-pepper-change-me",
            }
        );
        services.AddSingleton<IPasswordHasherService, PasswordHasherService>();
        services.AddSingleton<IEmailProtectionService, EmailProtectionService>();
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<AdminUserSeedService>();

        var provider = services.BuildServiceProvider();
        var contextFactory = provider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.EnsureCreatedAsync();
        }

        return new Fixture(connection, provider, provider.GetRequiredService<AdminUserSeedService>(), contextFactory);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _serviceProvider;

        public Fixture(
            SqliteConnection connection,
            ServiceProvider serviceProvider,
            AdminUserSeedService service,
            IDbContextFactory<OpenGitBaseDbContext> contextFactory
        )
        {
            _connection = connection;
            _serviceProvider = serviceProvider;
            Service = service;
            ContextFactory = contextFactory;
        }

        public AdminUserSeedService Service { get; }

        public IDbContextFactory<OpenGitBaseDbContext> ContextFactory { get; }

        public async ValueTask DisposeAsync()
        {
            await _serviceProvider.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
