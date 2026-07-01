namespace OpenGitBase.E2E.Core;

public static class ScenarioCatalog
{
    public static string CatalogPath =>
        Path.Combine(E2eEnvironment.RepoRoot, "docs", "e2e", "scenario-catalog.md");

    public static int CountDoneScenarios()
    {
        if (!File.Exists(CatalogPath))
        {
            return 0;
        }

        var count = 0;
        foreach (var line in File.ReadAllLines(CatalogPath))
        {
            if (line.StartsWith("| E2E-", StringComparison.Ordinal) && line.Contains("| done |", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
