namespace OpenGitBase.E2E.Core;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class E2eTierAttribute : Attribute
{
    public E2eTierAttribute(int tier)
    {
        Tier = tier;
    }

    public int Tier { get; }
}

public sealed class TierDefinition
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool FailFast { get; init; }

    public string[] Categories { get; init; } = Array.Empty<string>();
}

public interface ITierRegistry
{
    IReadOnlyList<TierDefinition> Tiers { get; }
}

public sealed class DefaultTierRegistry : ITierRegistry
{
    public IReadOnlyList<TierDefinition> Tiers { get; } =
    [
        new() { Id = 0, Name = "Infrastructure", FailFast = true, Categories = ["Tier0"] },
        new() { Id = 1, Name = "Auth", FailFast = true, Categories = ["Auth"] },
        new() { Id = 2, Name = "GitHttps", FailFast = false, Categories = ["GitHttps"] },
        new() { Id = 3, Name = "Security", FailFast = false, Categories = ["Security"] },
        new() { Id = 4, Name = "Discussion", FailFast = false, Categories = ["Discussion"] },
        new() { Id = 5, Name = "Repository", FailFast = false, Categories = ["Repository", "RepositoryMember", "Organization"] },
        new() { Id = 6, Name = "MergeRequest", FailFast = false, Categories = ["MergeRequest"] },
        new() { Id = 7, Name = "HaChaos", FailFast = false, Categories = ["HaChaos"] },
        new() { Id = 8, Name = "UI", FailFast = false, Categories = ["UI"] },
        new() { Id = 9, Name = "Fuzz", FailFast = false, Categories = ["Fuzz"] },
    ];
}

public sealed class TierOrchestrator
{
    private readonly ITierRegistry _registry;

    public TierOrchestrator(ITierRegistry? registry = null)
    {
        _registry = registry ?? new DefaultTierRegistry();
    }

    public IReadOnlyList<TierDefinition> Tiers => _registry.Tiers;

    public IReadOnlyList<TierSummary> BuildSkipSummaries(int failedTierId, string reason)
    {
        return _registry.Tiers
            .Where(t => t.Id > failedTierId)
            .Select(t => new TierSummary
            {
                Id = t.Id,
                Name = t.Name,
                Status = "Skipped",
                SkipReason = reason,
            })
            .ToList();
    }

    public string BuildDotnetTestFilter(int tierId, string? additionalFilter)
    {
        var tier = _registry.Tiers.FirstOrDefault(t => t.Id == tierId);
        var categories = tier?.Categories is { Length: > 0 } cats
            ? cats
            : [$"Tier{tierId}"];
        var categoryFilter = categories.Length == 1
            ? $"Category={categories[0]}"
            : $"({string.Join("|", categories.Select(c => $"Category={c}"))})";
        var filter = $"{categoryFilter}&Category!=Discovered";
        if (!string.IsNullOrWhiteSpace(additionalFilter))
        {
            filter = $"({filter})&({additionalFilter})";
        }

        return filter;
    }
}
