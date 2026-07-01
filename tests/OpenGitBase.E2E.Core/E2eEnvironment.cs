namespace OpenGitBase.E2E.Core;

public static class E2eEnvironment
{
    public const int GitHttpPort = 8089;

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
