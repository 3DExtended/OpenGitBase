using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenGitBase.Api.Services;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Entities;
using Testcontainers.PostgreSql;

namespace OpenGitBase.Api.Tests.Services;

/// <summary>
/// Every API replica seeds the admin user on startup. Against a fresh database both checked, both
/// inserted, and the loser failed with 23505 on "IX_Users_NormalizedUsername" (compose E2E runner,
/// 2026-10-18).
/// </summary>
public class AdminUserSeedConcurrencyTests
{
    private const int Replicas = 4;

    [RequiresDockerFact]
    public async Task StartAsync_ConcurrentReplicasOnFreshDatabase_SeedAdminAndOrganizationOnce()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16").Build();
        await postgres.StartAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Sql:ConnectionString"] = postgres.GetConnectionString();
        var featureAssemblies = FeatureRegistration.GetFeatureAssemblies();
        Assert.Equal(
            0,
            await DependencyInjectionHelpers.RunDatabaseMigrationsAsync(
                builder.Configuration,
                builder.Environment,
                featureAssemblies: featureAssemblies
            )
        );
        await using var databaseProvider = DependencyInjectionHelpers.BuildDatabaseServiceProvider(
            builder.Configuration,
            builder.Environment,
            featureAssemblies
        );
        var contextFactory = databaseProvider.GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>();
        var replicas = Enumerable.Range(0, Replicas).Select(_ => BuildReplica(contextFactory)).ToList();

        using var start = new Barrier(Replicas);
        await Task.WhenAll(
            replicas.Select(replica => Task.Run(async () =>
            {
                start.SignalAndWait();
                await replica.GetRequiredService<AdminUserSeedService>().StartAsync(CancellationToken.None);
            }))
        );

        await using var context = await contextFactory.CreateDbContextAsync();
        var user = await context.Set<UserEntity>().SingleAsync();
        Assert.Equal("ADMIN", user.NormalizedUsername);
        Assert.True(user.IsAdmin);
        Assert.Equal(user.Id, (await context.Set<UserCredentialsEntity>().SingleAsync()).UserId);
        var organization = await context.Set<OrganizationEntity>().SingleAsync();
        Assert.Equal("opengitbase", organization.Slug);
        Assert.Equal(user.Id, organization.OwnerUserId);
        Assert.Equal(organization.Id, (await context.Set<OrganizationMemberEntity>().SingleAsync()).OrganizationId);

        foreach (var replica in replicas)
        {
            await replica.DisposeAsync();
        }
    }

    private static ServiceProvider BuildReplica(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(contextFactory);
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
        services.AddSingleton(Options.Create(new AdminSeedOptions()));
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<AdminUserSeedService>();
        return services.BuildServiceProvider();
    }
}
