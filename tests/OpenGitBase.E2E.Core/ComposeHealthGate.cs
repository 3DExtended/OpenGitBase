namespace OpenGitBase.E2E.Core;

public static class ComposeHealthGate
{
    public static bool IsHealthy { get; private set; }

    public static string SkipReason { get; private set; } =
        "Compose stack not checked yet.";

    public static void Refresh()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = client.GetAsync(E2eEnvironment.ApiHealthUrl).GetAwaiter().GetResult();
            IsHealthy = response.IsSuccessStatusCode;
            SkipReason = IsHealthy
                ? string.Empty
                : $"API returned {(int)response.StatusCode} at {E2eEnvironment.ApiHealthUrl}.";
        }
        catch (Exception ex)
        {
            IsHealthy = false;
            SkipReason =
                $"Compose stack not reachable at {E2eEnvironment.ApiHealthUrl}. " +
                $"Run: dotnet run --project tests/OpenGitBase.E2E.Runner ({ex.Message})";
        }
    }
}
