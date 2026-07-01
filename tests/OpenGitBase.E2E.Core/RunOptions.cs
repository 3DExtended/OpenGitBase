namespace OpenGitBase.E2E.Core;

public enum ComposeProfile
{
    Fast,
    FullHa,
}

public enum OpenReportMode
{
    OpenOnFailure,
    OpenAlways,
    OpenNever,
}

public sealed record RunOptions
{
    public ComposeProfile Profile { get; init; } = ComposeProfile.Fast;

    public bool UpdateBaselines { get; init; }

    public OpenReportMode OpenReport { get; init; } = OpenReportMode.OpenOnFailure;

    public bool Fuzz { get; init; }

    public string? Filter { get; init; }

    public bool SkipCompose { get; init; }

    /// <summary>When set, only the given tier id runs (e.g. 8 for Playwright UI).</summary>
    public int? TierOnly { get; init; }

    public static RunOptions Parse(string[] args)
    {
        var options = new RunOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--update-baselines":
                    options = options with { UpdateBaselines = true };
                    break;
                case "--open-report":
                    options = options with { OpenReport = OpenReportMode.OpenAlways };
                    break;
                case "--no-open-report":
                    options = options with { OpenReport = OpenReportMode.OpenNever };
                    break;
                case "--fuzz":
                    options = options with { Fuzz = true };
                    break;
                case "--skip-compose":
                    options = options with { SkipCompose = true };
                    break;
                case "--profile" when i + 1 < args.Length:
                    options = options with
                    {
                        Profile = args[++i].ToLowerInvariant() switch
                        {
                            "full-ha" or "fullha" => ComposeProfile.FullHa,
                            _ => ComposeProfile.Fast,
                        },
                    };
                    break;
                case "--filter" when i + 1 < args.Length:
                    options = options with { Filter = args[++i] };
                    break;
                case "--tier" when i + 1 < args.Length && int.TryParse(args[i + 1], out var tierId):
                    options = options with { TierOnly = tierId };
                    i++;
                    break;
            }
        }

        return options;
    }
}

public sealed class RunResult
{
    public int ExitCode { get; init; }

    public string ReportPath { get; init; } = string.Empty;

    public IReadOnlyList<TierSummary> TierSummaries { get; init; } = Array.Empty<TierSummary>();
}

public sealed class TierSummary
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Status { get; init; } = "Pending";

    public int Passed { get; init; }

    public int Failed { get; init; }

    public int Skipped { get; init; }

    public string? SkipReason { get; init; }
}
