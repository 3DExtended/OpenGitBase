namespace OpenGitBase.Common.Options;

public class GoogleAuthOptions
{
    /// <summary>Single OAuth client id (legacy key <c>Google:ClientId</c>).</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth client ids whose ID tokens may sign in (<c>Google:ClientIds</c>).</summary>
    public string[] ClientIds { get; set; } = [];

    public IReadOnlyList<string> TrustedClientIds() =>
        ClientIds
            .Append(ClientId)
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}
