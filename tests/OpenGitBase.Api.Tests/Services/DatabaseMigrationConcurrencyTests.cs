using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using Testcontainers.PostgreSql;

namespace OpenGitBase.Api.Tests.Services;

/// <summary>
/// Both API replicas migrate on startup. Against a fresh database they raced and the loser failed
/// with 42P07 "relation already exists" (compose E2E runner, 2026-10-18).
/// </summary>
public class DatabaseMigrationConcurrencyTests
{
    private const int Replicas = 4;

    [RequiresDockerFact]
    public async Task RunDatabaseMigrationsAsync_ConcurrentReplicasOnFreshDatabase_AllSucceed()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16").Build();
        await postgres.StartAsync();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Sql:ConnectionString"] = postgres.GetConnectionString();
        var featureAssemblies = FeatureRegistration.GetFeatureAssemblies();

        using var start = new Barrier(Replicas);
        var exitCodes = await Task.WhenAll(
            Enumerable.Range(0, Replicas).Select(_ => Task.Run(() =>
            {
                start.SignalAndWait();
                return DependencyInjectionHelpers.RunDatabaseMigrationsAsync(
                    builder.Configuration,
                    builder.Environment,
                    featureAssemblies: featureAssemblies
                );
            }))
        );

        Assert.All(exitCodes, exitCode => Assert.Equal(0, exitCode));
        await using var provider = DependencyInjectionHelpers.BuildDatabaseServiceProvider(
            builder.Configuration,
            builder.Environment,
            featureAssemblies
        );
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}
