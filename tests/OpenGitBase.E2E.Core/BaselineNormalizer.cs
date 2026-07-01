using System.Text.RegularExpressions;

namespace OpenGitBase.E2E.Core;

public sealed class BaselineNormalizer
{
    private readonly Dictionary<string, string> _tokens = new(StringComparer.Ordinal);

    public BaselineNormalizer(string runSuffix)
    {
        RegisterToken("RUN_SUFFIX", runSuffix);
    }

    public void RegisterToken(string name, string value)
    {
        _tokens[name] = value;
    }

    public string Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var result = input;
        foreach (var (name, value) in _tokens)
        {
            if (!string.IsNullOrEmpty(value))
            {
                result = result.Replace(value, $"{{{{{name}}}}}", StringComparison.Ordinal);
            }
        }

        result = Regex.Replace(result, @"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})\b", "{{TIMESTAMP}}");
        result = Regex.Replace(result, @"""durationMs""\s*:\s*\d+", "\"durationMs\":{{DURATION_MS}}");
        result = Regex.Replace(result, @"""totalDurationMs""\s*:\s*\d+", "\"totalDurationMs\":{{DURATION_MS}}");
        result = Regex.Replace(result, @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", "{{GUID}}", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b[0-9a-f]{7,40}\b", "{{SHA}}", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "{{JWT}}");
        result = Regex.Replace(result, @"ogb_[A-Za-z0-9_-]+", "{{PAT}}");
        result = Regex.Replace(result, @"""traceId""\s*:\s*""[^""]+""", "\"traceId\":\"{{TRACE_ID}}\"", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"/(?:private/)?var/folders/[^\s|""]+", "{{TEMP_DIR}}");
        result = Regex.Replace(result, @"error: RPC failed; HTTP \d+[^\n|]*", "error: {{GIT_RPC_ERROR}}");
        result = Regex.Replace(result, @"fatal: [^\n|]+", "fatal: {{GIT_FATAL}}");
        result = Regex.Replace(result, @"verify\?token=[^""\s&]+", "verify?token={{VERIFY_TOKEN}}");
        return result;
    }
}
