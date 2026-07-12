using System.Security.Cryptography;
using System.Text;

namespace OpenGitBase.Pipeline;

public static class DependencyRecipeKeys
{
    public static string Compute(string baseSlug, string installScript)
    {
        var normalizedBase = Normalize(baseSlug);
        var normalizedScript = Normalize(installScript);
        var payload = normalizedBase + normalizedScript;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var lines = normalized.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            lines[index] = lines[index].TrimEnd();
        }

        return string.Join('\n', lines).Trim();
    }
}
