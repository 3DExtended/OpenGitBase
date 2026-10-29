using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.DependencyInjection;

namespace OpenGitBase.Common.Tests;

public class DependencyInjectionHelpersTests
{
    [Fact]
    public async Task ConfigureGlobalServices_E2ETestEnvironment_SkipsDatabaseConfiguration()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateEnvironment("E2ETest"));

        await DependencyInjectionHelpers.ConfigureGlobalServices(
            services,
            new ConfigurationBuilder().Build(),
            Array.Empty<Assembly>()
        );

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IFeatureAssemblyProvider>());
        Assert.NotNull(provider.GetService<IQueryProcessor>());
        Assert.Null(provider.GetService<IDbContextFactory<OpenGitBaseDbContext>>());
    }

    [Fact]
    public async Task ConfigureGlobalServices_WithOptionalSections_RegistersConfiguredServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateEnvironment("Development"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SendGrid:ApiKey"] = "test-key",
                    ["SendGrid:FromEmailAddress"] = "noreply@example.com",
                    ["SendGrid:FromSenderName"] = "OpenGitBase",
                    ["Encryption:DataKey"] = Convert.ToBase64String(new byte[32]),
                    ["Encryption:Pepper"] = "pepper",
                }
            )
            .Build();

        await DependencyInjectionHelpers.ConfigureGlobalServices(
            services,
            configuration,
            Array.Empty<Assembly>()
        );

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<SendGridOptions>());
        Assert.NotNull(provider.GetService<EncryptionOptions>());
        Assert.NotNull(provider.GetService<ISendGridEmailSender>());
    }

    [Fact]
    public async Task ConfigureGlobalServices_WithoutSqlConnectionString_DoesNotRegisterDatabaseFactory()
    {
        var services = new ServiceCollection();
        services.AddSingleton(CreateEnvironment("Development"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Sql:ConnectionString"] = " " })
            .Build();

        await DependencyInjectionHelpers.ConfigureGlobalServices(
            services,
            configuration,
            Array.Empty<Assembly>()
        );

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IDbContextFactory<OpenGitBaseDbContext>>());
    }

    [Fact]
    public async Task ConfigureGlobalServices_UnderEfDesignTime_RegistersDatabaseWithoutMigrating()
    {
        // `dotnet ef migrations add` builds the API host for its DbContext and must leave the
        // configured database alone. Nothing listens on port 1, so a migration attempt would fail
        // with "Database migration failed during API startup."
        var services = new ServiceCollection();
        services.AddSingleton(CreateEnvironment("Development"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sql:ConnectionString"] = "Host=127.0.0.1;Port=1;Database=ogb;Username=ogb",
                }
            )
            .Build();

        var wasDesignTime = EF.IsDesignTime;
        EF.IsDesignTime = true;
        try
        {
            await DependencyInjectionHelpers.ConfigureGlobalServices(
                services,
                configuration,
                Array.Empty<Assembly>()
            );
        }
        finally
        {
            EF.IsDesignTime = wasDesignTime;
        }

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IDbContextFactory<OpenGitBaseDbContext>>());
    }

    private static IHostEnvironment CreateEnvironment(string environmentName) =>
        new TestHostEnvironment(environmentName);
}
