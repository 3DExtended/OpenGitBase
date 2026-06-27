#pragma warning disable SA1202 // Elements should be ordered by access
using System.Text.RegularExpressions;

namespace OpenGitBase.Features.MergeRequest;

internal static partial class MergeRequestDiscussionLinkBodyParser
{
    [GeneratedRegex(@"(?<![\w/])#(\d+)\b", RegexOptions.CultureInvariant)]
    private static partial Regex DiscussionReferencePattern();

    public static IReadOnlyList<int> ParseDiscussionNumbers(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        return DiscussionReferencePattern()
            .Matches(body)
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(number => number)
            .ToList();
    }
}
