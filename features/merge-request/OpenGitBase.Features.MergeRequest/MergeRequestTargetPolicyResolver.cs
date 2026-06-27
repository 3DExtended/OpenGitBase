#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.MergeRequest;

public static class MergeRequestTargetPolicyResolver
{
    public const string DefaultBranchPatternAlias = "@default";

    public static MergeRequestTargetPolicy Resolve(
        string targetRef,
        string? defaultBranchName,
        IReadOnlyList<ProtectedBranchRuleDto> rules
    )
    {
        var branchName = NormalizeBranchName(targetRef);
        if (branchName is null || rules.Count == 0)
        {
            return MergeRequestTargetPolicy.Unprotected;
        }

        var matching = rules
            .Where(rule => Matches(branchName, rule.Pattern, defaultBranchName))
            .ToList();

        if (matching.Count == 0)
        {
            return MergeRequestTargetPolicy.Unprotected;
        }

        return new MergeRequestTargetPolicy
        {
            RequiredApprovalCount = matching.Max(rule => rule.RequiredApprovalCount),
            DismissApprovalsOnPush = matching.Any(rule => rule.DismissApprovalsOnPush),
            MergeRoleThreshold = matching.Max(rule => rule.MergeRoleThreshold),
            LockedMergeStrategy = matching
                .Select(rule => rule.LockedMergeStrategy)
                .FirstOrDefault(strategy => strategy is not null),
        };
    }

    internal static string? NormalizeBranchName(string refName)
    {
        var trimmed = refName.Trim();
        const string headsPrefix = "refs/heads/";
        if (trimmed.StartsWith(headsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[headsPrefix.Length..];
        }

        return trimmed;
    }

    internal static bool Matches(string branchName, string pattern, string? defaultBranchName)
    {
        var resolvedPattern = ResolvePattern(pattern, defaultBranchName);
        if (string.Equals(resolvedPattern, branchName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (resolvedPattern.EndsWith('*'))
        {
            var prefix = resolvedPattern[..^1];
            return branchName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    internal static string ResolvePattern(string pattern, string? defaultBranchName)
    {
        if (string.Equals(pattern, DefaultBranchPatternAlias, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(defaultBranchName) ? pattern : defaultBranchName.Trim();
        }

        return pattern.Trim();
    }
}
