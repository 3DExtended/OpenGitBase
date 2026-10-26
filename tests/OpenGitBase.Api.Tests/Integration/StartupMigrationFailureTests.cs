using System.Diagnostics;
using Npgsql;
using OpenGitBase.Api.Tests.Base;
using Testcontainers.PostgreSql;

namespace OpenGitBase.Api.Tests.Integration;

/// <summary>
/// A failed startup migration must end the API process with an exit code. An exception that escapes
/// Main aborts the runtime with SIGABRT, which the kernel drops for PID 1 in a container, so the API
/// kept running unhealthy and Docker never restarted it (compose E2E runner, 2026-10-18).
/// </summary>
public class StartupMigrationFailureTests
{
    [RequiresDockerFact]
    public async Task Api_WhenStartupMigrationFails_ExitsWithCodeOne()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16").Build();
        await postgres.StartAsync();
        await using (var connection = new NpgsqlConnection(postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            // The initial migrations create "Users", so an existing table fails them with 42P07.
            await using var command = new NpgsqlCommand("CREATE TABLE \"Users\" (\"Id\" integer)", connection);
            await command.ExecuteNonQueryAsync();
        }

        var apiAssembly = typeof(ApiEntryPoint).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(apiAssembly)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(apiAssembly);
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:0";
        startInfo.Environment["GIT_SSH_ENABLED"] = "false";
        startInfo.Environment["Sql__ConnectionString"] = postgres.GetConnectionString();

        using var api = Process.Start(startInfo)!;
        var standardOutput = api.StandardOutput.ReadToEndAsync();
        var standardError = api.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await api.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            api.Kill(entireProcessTree: true);
            Assert.Fail("The API kept running after its startup migration failed.");
        }

        Assert.True(
            api.ExitCode == 1,
            $"Exit code {api.ExitCode}.\n{await standardOutput}\n{await standardError}"
        );
        Assert.Contains("Database migration failed during API startup.", await standardError);
    }
}
