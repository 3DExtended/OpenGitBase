using System.Diagnostics;
using System.Reflection;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.QueryHandlers.HealthCheck;
using OpenGitBase.Common.SendGrid;
using OpenGitBase.Common.SendGrid.QueryHandlers;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs.DependencyInjection;

namespace OpenGitBase.Common;

public static class DependencyInjectionHelpers
{
    public static Task ConfigureGlobalServices(
        IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyList<Assembly>? featureAssemblies = null
    )
    {
        var assemblies = featureAssemblies ?? Array.Empty<Assembly>();
        services.AddSingleton(configuration);
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider(assemblies));

        var env = services.BuildServiceProvider().GetRequiredService<IHostEnvironment>();

        if (!env.IsEnvironment("E2ETest"))
        {
            ConfigureDatabase(services, configuration, env);
        }

        if (assemblies.Count > 0)
        {
            TypeAdapterConfig.GlobalSettings.Scan(assemblies.ToArray());
        }

        services.AddMapster();
        ConfigureSendGrid(services, configuration);
        // agentGenCli:sendgrid-di
        ConfigureEncryption(services, configuration);
        // agentGenCli:auth-di
        AddCqrs(services, assemblies);

        if (!env.IsEnvironment("E2ETest"))
        {
            return ApplyEfCoreMigrations(services, assemblies);
        }

        return Task.CompletedTask;
    }

    public static async Task<int> RunDatabaseMigrationsAsync(
        IConfiguration configuration,
        IHostEnvironment env,
        bool requireDatabase = true,
        IReadOnlyList<Assembly>? featureAssemblies = null,
        CancellationToken cancellationToken = default
    )
    {
        if (env.IsEnvironment("E2ETest"))
        {
            return 0;
        }

        var services = new ServiceCollection();
        services.AddSingleton(env);
        services.AddSingleton(configuration);
        services.AddLogging();

        var assemblies = featureAssemblies ?? Array.Empty<Assembly>();
        services.AddSingleton<IFeatureAssemblyProvider>(new FeatureAssemblyProvider(assemblies));

        ConfigureDatabase(services, configuration, env);

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IDbContextFactory<OpenGitBaseDbContext>>();

        if (factory == null)
        {
            if (!requireDatabase)
            {
                return 0;
            }

            Console.Error.WriteLine("Database migrations skipped: no Sql connection configured.");
            return 1;
        }

        try
        {
            await using var context = await factory.CreateDbContextAsync(cancellationToken);
            context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(
                cancellationToken
            );

            if (pendingMigrations.Any())
            {
                Console.WriteLine("Applying pending migrations...");

                await context.Database.MigrateAsync(cancellationToken);
                Console.WriteLine("Migrations applied successfully.");
            }
            else
            {
                Console.WriteLine("No pending migrations.");
            }

            context.Database.SetCommandTimeout(TimeSpan.FromSeconds(30));

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Migration failed: {ex.Message}");
            return 1;
        }
    }

    private static void ConfigureDatabase(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment env
    )
    {
        var sqlOptions = configuration.GetSection("Sql").Get<SqlOptions>();
        if (sqlOptions == null || string.IsNullOrWhiteSpace(sqlOptions.ConnectionString))
        {
            return;
        }

        services.AddSingleton(sqlOptions);

        services.AddDbContextFactory<OpenGitBaseDbContext>(
            (serviceProvider, options) =>
            {
                options
                    .UseNpgsql(
                        sqlOptions.ConnectionString,
                        npgsql =>
                        {
                            npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                            npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                        }
                    )
                    .EnableServiceProviderCaching(false);

                if (!env.IsProduction())
                {
                    options.EnableDetailedErrors();
                }
            }
        );
    }

    private static void ConfigureSendGrid(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ISendGridEmailSender, SendGridEmailSender>();

        var sendGridOptions = configuration.GetSection("SendGrid").Get<SendGridOptions>();
        if (sendGridOptions == null)
        {
            return;
        }

        services.AddSingleton(sendGridOptions);
    }

    private static void ConfigureEncryption(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        var encryptionOptions = configuration.GetSection("Encryption").Get<EncryptionOptions>();
        if (encryptionOptions != null)
        {
            services.AddSingleton(encryptionOptions);
        }
    }

    private static void AddCqrs(
        IServiceCollection services,
        IReadOnlyList<Assembly> featureAssemblies
    )
    {
        services.AddCqrs(options =>
        {
            options.WithQueryHandlersFrom(typeof(SystemHealthCheckQueryHandler).Assembly);
            options.WithQueryHandlersFrom(typeof(EmailSendQueryHandler).Assembly);
            foreach (var assembly in featureAssemblies)
            {
                options.WithQueryHandlersFrom(assembly);
            }
        });
    }

    private static async Task ApplyEfCoreMigrations(
        IServiceCollection services,
        IReadOnlyList<Assembly> featureAssemblies
    )
    {
        if (Debugger.IsAttached)
        {
            return;
        }

        var env = services.BuildServiceProvider().GetRequiredService<IHostEnvironment>();
        if (env.IsEnvironment("E2ETest"))
        {
            return;
        }

        var isEfMigrations = Array.Exists(
            Environment.GetCommandLineArgs(),
            arg => arg.Contains("ef", StringComparison.OrdinalIgnoreCase)
        );

        if (isEfMigrations)
        {
            return;
        }

        var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
        var exitCode = await RunDatabaseMigrationsAsync(
            configuration,
            env,
            requireDatabase: false,
            featureAssemblies: featureAssemblies
        );
        if (exitCode != 0)
        {
            throw new InvalidOperationException("Database migration failed during API startup.");
        }
    }
}
