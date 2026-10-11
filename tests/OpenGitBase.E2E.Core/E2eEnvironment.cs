namespace OpenGitBase.E2E.Core;

public static class E2eEnvironment
{
    /// <summary>Host port of the compose stack's HAProxy frontend (API + Git Smart HTTP).</summary>
    public const int DefaultGitHttpPort = 8089;

    /// <summary>Container names are <c>{prefix}{service with '-' as '_'}</c>, as in docker-compose.yml.</summary>
    public const string DefaultContainerPrefix = "opengitbase_";

    /// <summary>
    /// <see cref="DefaultGitHttpPort"/> unless <c>OPENGITBASE_E2E_HTTP_PORT</c> names another one, for a
    /// stack that cannot publish 8089 on this machine. Baselines always record the default port
    /// (<see cref="BaselineNormalizer"/>).
    /// </summary>
    public static int GitHttpPort { get; } = ReadPort("OPENGITBASE_E2E_HTTP_PORT", DefaultGitHttpPort);

    /// <summary>
    /// <see cref="DefaultContainerPrefix"/> unless <c>OPENGITBASE_E2E_CONTAINER_PREFIX</c> names another
    /// one, for a stack run under its own container names next to another compose project.
    /// </summary>
    public static string ContainerPrefix { get; } =
        Environment.GetEnvironmentVariable("OPENGITBASE_E2E_CONTAINER_PREFIX") is { Length: > 0 } prefix
            ? prefix
            : DefaultContainerPrefix;

    public static string RepoRoot { get; } = FindRepoRoot();

    public static Uri ApiBaseUrl { get; } = new($"http://localhost:{GitHttpPort}/api/");

    public static Uri ApiHealthUrl { get; } = new($"http://localhost:{GitHttpPort}/health");

    public static Uri WebBaseUrl { get; } = new("http://localhost:3000");

    public static Uri GitHttpBaseUrl { get; } = new($"http://127.0.0.1:{GitHttpPort}");

    public static string ReportsDirectory { get; } = Path.Combine(RepoRoot, ".e2e-reports");

    public static string BaselinesRoot { get; } = Path.Combine(RepoRoot, "tests", "OpenGitBase.E2E.Tests", "Baselines");

    public static bool SkipCompose =>
        string.Equals(Environment.GetEnvironmentVariable("OPENGITBASE_E2E_SKIP_COMPOSE"), "1", StringComparison.Ordinal);

    public static string BuildPatRemoteUrl(string username, string repoSlug, string pat) =>
        $"http://git:{Uri.EscapeDataString(pat)}@127.0.0.1:{GitHttpPort}/{username}/{repoSlug}.git";

    /// <summary>The container of compose service <paramref name="serviceName"/>, e.g. storage-2.</summary>
    public static string ContainerName(string serviceName) =>
        ContainerPrefix + serviceName.Replace('-', '_');

    private static int ReadPort(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out var port) && port is > 0 and < 65536
            ? port
            : fallback;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OpenGitBase.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
