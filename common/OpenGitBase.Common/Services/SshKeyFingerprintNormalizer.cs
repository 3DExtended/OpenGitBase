namespace OpenGitBase.Common.Services;

public static class SshKeyFingerprintNormalizer
{
    public const string Sha256Prefix = "SHA256:";

    /// <summary>
    /// Formats a SHA-256 digest as OpenSSH does: "SHA256:" + unpadded base64.
    /// </summary>
    public static string ToOpenSshSha256(string rawBase64Fingerprint) =>
        $"{Sha256Prefix}{TrimBase64Padding(rawBase64Fingerprint)}";

    /// <summary>
    /// Returns fingerprint variants for DB lookup. OpenSSH omits base64 padding; legacy
    /// rows may include trailing "=" from .NET <see cref="Convert.ToBase64String(byte[])"/>.
    /// </summary>
    public static IReadOnlyList<string> GetLookupCandidates(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return [];
        }

        var trimmed = fingerprint.Trim();
        var raw = trimmed.StartsWith(Sha256Prefix, StringComparison.Ordinal)
            ? trimmed[Sha256Prefix.Length..]
            : trimmed;

        if (string.IsNullOrEmpty(raw))
        {
            return [trimmed];
        }

        var unpaddedRaw = TrimBase64Padding(raw);
        var candidates = new HashSet<string>(StringComparer.Ordinal)
        {
            $"{Sha256Prefix}{unpaddedRaw}",
            unpaddedRaw,
        };

        foreach (var paddedRaw in GetBase64PaddingVariants(unpaddedRaw))
        {
            if (paddedRaw != unpaddedRaw)
            {
                candidates.Add($"{Sha256Prefix}{paddedRaw}");
                candidates.Add(paddedRaw);
            }
        }

        return [.. candidates];
    }

    private static string TrimBase64Padding(string value) => value.TrimEnd('=');

    private static IEnumerable<string> GetBase64PaddingVariants(string unpadded)
    {
        var remainder = unpadded.Length % 4;
        if (remainder == 0)
        {
            yield break;
        }

        var padCount = 4 - remainder;
        yield return unpadded + new string('=', padCount);
    }
}
