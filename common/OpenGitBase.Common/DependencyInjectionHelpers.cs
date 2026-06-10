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
            return ApplyEfCoreMigrations(services);
        }

        return Task.CompletedTask;
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

        services.AddDbContextFactory<OpenGitBaseDbContext>(options =>
        {
            options.UseNpgsql(
                sqlOptions.ConnectionString,
                npgsql =>
                {
                    npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                    npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                }
            );

            if (!env.IsProduction())
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
            }

            options.LogTo(
                Console.WriteLine,
                new[]
                {
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted,
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandError,
                },
                LogLevel.Information
            );
        });
    }

    private static void ConfigureSendGrid(IServiceCollection services, IConfiguration configuration)
    {
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

    private static async Task ApplyEfCoreMigrations(IServiceCollection services)
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

        await using var scope = services.BuildServiceProvider().CreateAsyncScope();
        var factory = scope.ServiceProvider.GetService<IDbContextFactory<OpenGitBaseDbContext>>();

        if (factory == null)
        {
            return;
        }

        await using var context = await factory.CreateDbContextAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        if (pendingMigrations.Any())
        {
            Console.WriteLine("Applying pending migrations...");
            await context.Database.MigrateAsync();
            Console.WriteLine("Migrations applied successfully.");
        }
    }
}
