namespace OpenGitBase.Pipeline;

public static class OnlyGlobMatcher
{
    public static bool IsSupportedPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        return !pattern.Contains("**", StringComparison.Ordinal);
    }

    public static bool IsMatch(string pattern, string value)
    {
        if (!IsSupportedPattern(pattern))
        {
            return false;
        }

        var parts = pattern.Split('*');
        var index = 0;
        var anchoredStart = !pattern.StartsWith('*');
        var anchoredEnd = !pattern.EndsWith('*');

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0)
            {
                continue;
            }

            var found = value.IndexOf(part, index, StringComparison.Ordinal);
            if (found < 0)
            {
                return false;
            }

            if (i == 0 && anchoredStart && found != 0)
            {
                return false;
            }

            index = found + part.Length;
        }

        if (!anchoredEnd)
        {
            return true;
        }

        var lastPart = parts.LastOrDefault(part => part.Length > 0) ?? string.Empty;
        return value.EndsWith(lastPart, StringComparison.Ordinal);
    }
}
