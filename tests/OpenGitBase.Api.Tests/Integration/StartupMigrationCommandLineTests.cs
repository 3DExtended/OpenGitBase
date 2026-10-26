using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Common;
using OpenGitBase.Common.Data;
using Testcontainers.PostgreSql;

namespace OpenGitBase.Api.Tests.Integration;

/// <summary>
/// Startup skipped its migrations whenever any command-line argument contained "ef", a guess at
/// running under <c>dotnet ef</c>. An API started as <c>dotnet /home/stefan/app/OpenGitBase.Api.dll</c>,
/// from <c>/srv/reference/...</c> or with a <c>--urls</c> value containing "ef" then came up
/// against an unmigrated database without a word. The command line belongs to the process, so this
/// starts the real API with one.
/// </summary>
public class StartupMigrationCommandLineTests
{
    [RequiresDockerFact]
    public async Task Api_WithEfInItsCommandLine_AppliesPendingMigrationsOnStartup()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16").Build();
        await postgres.StartAsync();

        var apiAssembly = typeof(ApiEntryPoint).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(apiAssembly);
        startInfo.ArgumentList.Add("/home/stefan/app/OpenGitBase.Api.dll");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        startInfo.Environment["GIT_SSH_ENABLED"] = "false";
        startInfo.Environment["Sql__ConnectionString"] = postgres.GetConnectionString();

        var output = new StringBuilder();
        var startupPassedMigrations = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        using var api = new Process { StartInfo = startInfo };
        api.OutputDataReceived += (_, line) =>
        {
            if (line.Data == null)
            {
                startupPassedMigrations.TrySetResult();
                return;
            }

            lock (output)
            {
                output.AppendLine(line.Data);
            }

            // Startup migrates before anything else, so once the host has started it either
            // migrated or skipped.
            if (
                line.Data.Contains("Migrations applied successfully.")
                || line.Data.Contains("Application started.")
            )
            {
                startupPassedMigrations.TrySetResult();
            }
        };
        api.ErrorDataReceived += (_, line) =>
        {
            lock (output)
            {
                output.AppendLine(line.Data);
            }
        };

        api.Start();
        api.BeginOutputReadLine();
        api.BeginErrorReadLine();
        try
        {
            await Task.WhenAny(startupPassedMigrations.Task, Task.Delay(TimeSpan.FromMinutes(2)));
        }
        finally
        {
            api.Kill(entireProcessTree: true);
            await api.WaitForExitAsync();
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["Sql:ConnectionString"] = postgres.GetConnectionString();
        await using var provider = DependencyInjectionHelpers.BuildDatabaseServiceProvider(
            builder.Configuration,
            builder.Environment,
            FeatureRegistration.GetFeatureAssemblies()
        );
        await using var context = await provider
            .GetRequiredService<IDbContextFactory<OpenGitBaseDbContext>>()
            .CreateDbContextAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        string apiOutput;
        lock (output)
        {
            apiOutput = output.ToString();
        }

        Assert.True(
            !pendingMigrations.Any(),
            $"{pendingMigrations.Count()} migrations still pending.\n{apiOutput}"
        );
    }
}
