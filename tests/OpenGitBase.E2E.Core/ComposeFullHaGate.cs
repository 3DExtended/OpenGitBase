namespace OpenGitBase.E2E.Core;

public static class ComposeFullHaGate
{
    public const string ProfileEnvironmentVariable = "OPENGITBASE_E2E_COMPOSE_PROFILE";

    public static bool IsFullHaProfile { get; private set; }

    public static string SkipReason { get; private set; } =
        "Full-HA profile not active.";

    public static void Refresh()
    {
        var profile = Environment.GetEnvironmentVariable(ProfileEnvironmentVariable);
        IsFullHaProfile = string.Equals(profile, nameof(ComposeProfile.FullHa), StringComparison.OrdinalIgnoreCase)
            || string.Equals(profile, "full-ha", StringComparison.OrdinalIgnoreCase);
        SkipReason = IsFullHaProfile
            ? string.Empty
            : "Scenario requires full-HA compose profile. Run with: dotnet run --project tests/OpenGitBase.E2E.Runner -- --profile full-ha";
    }
}
