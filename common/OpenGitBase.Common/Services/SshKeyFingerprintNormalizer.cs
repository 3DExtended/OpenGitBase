namespace OpenGitBase.Common.Services;

public static class SshKeyFingerprintNormalizer
{
    public const string Sha256Prefix = "SHA256:";

    public static string ToOpenSshSha256(string rawBase64Fingerprint) =>
        $"{Sha256Prefix}{rawBase64Fingerprint}";

    public static IReadOnlyList<string> GetLookupCandidates(string? fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return [];
        }

        var trimmed = fingerprint.Trim();
        if (trimmed.StartsWith(Sha256Prefix, StringComparison.Ordinal))
        {
            var raw = trimmed[Sha256Prefix.Length..];
            return string.IsNullOrEmpty(raw) ? [trimmed] : [trimmed, raw];
        }

        return [trimmed, ToOpenSshSha256(trimmed)];
    }
}
