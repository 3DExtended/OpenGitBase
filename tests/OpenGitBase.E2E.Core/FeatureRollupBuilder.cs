namespace OpenGitBase.E2E.Core;

public sealed class FeatureSummary
{
    public string FeatureCode { get; init; } = string.Empty;

    public string FeatureName { get; init; } = string.Empty;

    public int Passed { get; init; }

    public int Failed { get; init; }

    public int Skipped { get; init; }

    public string Status { get; init; } = "Pending";

    public IReadOnlyList<int> TierIds { get; init; } = Array.Empty<int>();
}

public static class FeatureRollupBuilder
{
    private static readonly (string Code, string Name, int[] TierIds)[] FeatureMap =
    [
        ("F00", "Infrastructure", [0]),
        ("F01", "Auth", [1]),
        ("F08", "Git HTTPS", [2]),
        ("SEC", "Security", [3]),
        ("F06", "Discussion", [4]),
        ("F05", "Browse", [5]),
        ("F07", "Merge requests", [6]),
        ("F10", "HA storage", [7]),
        ("F12", "UI / discovery", [8]),
        ("FUZZ", "Fuzz", [9]),
    ];

    public static IReadOnlyList<FeatureSummary> Build(IReadOnlyList<TierSummary> tiers)
    {
        var byId = tiers.ToDictionary(t => t.Id);
        var summaries = new List<FeatureSummary>();
        foreach (var (code, name, tierIds) in FeatureMap)
        {
            var matched = tierIds.Select(id => byId.GetValueOrDefault(id)).Where(t => t != null).Cast<TierSummary>().ToList();
            if (matched.Count == 0)
            {
                continue;
            }

            summaries.Add(new FeatureSummary
            {
                FeatureCode = code,
                FeatureName = name,
                Passed = matched.Sum(t => t.Passed),
                Failed = matched.Sum(t => t.Failed),
                Skipped = matched.Sum(t => t.Skipped),
                Status = matched.Any(t => t.Status == "Failed")
                    ? "Failed"
                    : matched.All(t => t.Status is "Passed" or "Skipped")
                        ? matched.Any(t => t.Status == "Passed") ? "Passed" : "Skipped"
                        : "Pending",
                TierIds = tierIds,
            });
        }

        return summaries;
    }
}
