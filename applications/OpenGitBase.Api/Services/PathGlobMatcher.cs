﻿namespace OpenGitBase.Api.Services;

public static class PathGlobMatcher
{
    public static bool IsMatch(string path, string glob)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(glob))
        {
            return false;
        }

        var normalizedPath = path.Replace('\\', '/').TrimStart('/');
        var normalizedGlob = glob.Replace('\\', '/').TrimStart('/');

        if (normalizedGlob.StartsWith("**/"))
        {
            normalizedGlob = normalizedGlob[3..];
        }

        if (normalizedGlob.Contains('*'))
        {
            return MatchesWildcard(normalizedPath, normalizedGlob);
        }

        return string.Equals(normalizedPath, normalizedGlob, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesWildcard(string path, string pattern)
    {
        if (pattern.StartsWith('*'))
        {
            var suffix = pattern[1..];
            return path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.EndsWith('*'))
        {
            var prefix = pattern[..^1];
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        var parts = pattern.Split('*', StringSplitOptions.None);
        if (parts.Length == 1)
        {
            return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);
        }

        var index = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            var matchIndex = path.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
            {
                return false;
            }

            if (i == 0 && matchIndex != 0)
            {
                return false;
            }

            index = matchIndex + part.Length;
        }

        var lastPart = parts[^1];
        return lastPart.Length == 0
            || path.EndsWith(lastPart, StringComparison.OrdinalIgnoreCase);
    }
}
