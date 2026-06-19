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

    public static Task<int> RunAsync(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        return OpenGitBase.Common.DependencyInjectionHelpers.RunDatabaseMigrationsAsync(
            builder.Configuration,
            builder.Environment,
            featureAssemblies: FeatureRegistration.GetFeatureAssemblies()
        );
    }
}
