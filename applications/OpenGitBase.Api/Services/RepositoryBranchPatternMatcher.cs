namespace OpenGitBase.Api.Services;

public static class RepositoryBranchPatternMatcher
{
    public static string ResolvePattern(string pattern, string? defaultBranchName)
    {
        if (string.Equals(pattern, DefaultRefResolver.DefaultBranchPatternAlias, StringComparison.OrdinalIgnoreCase))
        {
            return defaultBranchName ?? string.Empty;
        }

        return pattern;
    }

    public static bool Matches(string branchName, string pattern, string? defaultBranchName)
    {
        var resolved = ResolvePattern(pattern, defaultBranchName);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return false;
        }

        if (resolved.Contains('*', StringComparison.Ordinal))
        {
            return MatchesWildcard(branchName, resolved);
        }

        return string.Equals(branchName, resolved, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesWildcard(string branchName, string pattern)
    {
        if (pattern.EndsWith("/*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^2];
            return branchName.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(branchName, prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(branchName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
