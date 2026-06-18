namespace OpenGitBase.Api;

internal static class MigrateOnlyHost
{
    public const string EnvironmentVariableName = "OPENGITBASE_MIGRATE_ONLY";

    public static bool IsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        return await OpenGitBase.Common.DependencyInjectionHelpers.RunDatabaseMigrationsAsync(
            builder.Configuration,
            builder.Environment
        );
    }
}
