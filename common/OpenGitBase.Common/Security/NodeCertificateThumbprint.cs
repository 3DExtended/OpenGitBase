namespace OpenGitBase.Common.Security;

public static class NodeCertificateThumbprint
{
    public static string Normalize(string thumbprint)
    {
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return string.Empty;
        }

        return thumbprint
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
    }

    public static bool Matches(string left, string right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
}
