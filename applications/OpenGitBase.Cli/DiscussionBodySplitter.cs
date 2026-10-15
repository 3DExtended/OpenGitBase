namespace OpenGitBase.Cli;

/// <summary>
/// Splits a long discussion body into the opening body plus continuation comments that fit the
/// forge's column limits. Splits land on blank lines outside code fences, so re-joining the parts
/// with a blank line (as <c>ogb docs pull</c> does) restores the original text. A single paragraph
/// longer than the limit falls back to a line break, then a space, then a hard cut.
/// </summary>
public static class DiscussionBodySplitter
{
    /// <summary>Mirrors <c>DiscussionEntityConfiguration</c>: <c>Body</c> HasMaxLength(8000).</summary>
    public const int MaxBodyLength = 8000;

    /// <summary>Mirrors <c>DiscussionEntityConfiguration</c>: comment <c>BodyMarkdown</c> HasMaxLength(16000).</summary>
    public const int MaxCommentLength = 16000;

    public static IReadOnlyList<string> Split(string? body)
    {
        var remaining = body?.Trim() ?? string.Empty;
        if (remaining.Length == 0)
        {
            return [];
        }

        var parts = new List<string>();
        var limit = MaxBodyLength;
        while (remaining.Length > limit)
        {
            var cut = FindCut(remaining, limit);
            parts.Add(remaining[..cut].TrimEnd());
            remaining = remaining[cut..].TrimStart('\r', '\n');
            limit = MaxCommentLength;
        }

        parts.Add(remaining);
        return parts;
    }

    private static int FindCut(string text, int limit)
    {
        var paragraphCut = -1;
        var fencedParagraphCut = -1;
        var lineCut = -1;
        var inFence = false;

        for (var lineStart = 0; lineStart < text.Length;)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0 || lineEnd > limit)
            {
                break;
            }

            if (IsFenceDelimiter(text.AsSpan(lineStart, lineEnd - lineStart)))
            {
                inFence = !inFence;
            }

            if (lineEnd > 0)
            {
                lineCut = lineEnd;
                if (IsBlankLineAt(text, lineEnd + 1))
                {
                    if (inFence)
                    {
                        fencedParagraphCut = lineEnd;
                    }
                    else
                    {
                        paragraphCut = lineEnd;
                    }
                }
            }

            lineStart = lineEnd + 1;
        }

        if (paragraphCut > 0)
        {
            return paragraphCut;
        }

        if (fencedParagraphCut > 0)
        {
            return fencedParagraphCut;
        }

        if (lineCut > 0)
        {
            return lineCut;
        }

        var spaceCut = text.LastIndexOf(' ', limit - 1);
        if (spaceCut > 0)
        {
            return spaceCut;
        }

        return char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
    }

    private static bool IsFenceDelimiter(ReadOnlySpan<char> line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith("~~~", StringComparison.Ordinal);
    }

    private static bool IsBlankLineAt(string text, int index) =>
        index < text.Length
        && (text[index] == '\n'
            || (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n'));
}
