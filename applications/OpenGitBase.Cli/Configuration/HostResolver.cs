namespace OpenGitBase.Cli.Configuration;

public sealed class HostResolver : IHostResolver
{
    public string DefaultHost => HostDefaults.ProductionHost;

    public string NormalizeHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host must not be empty.", nameof(host));
        }

        var trimmed = host.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        return trimmed;
    }

    public string ResolveHost(string? hostnameOverride, string? configuredHost)
    {
        if (!string.IsNullOrWhiteSpace(hostnameOverride))
        {
            return NormalizeHost(hostnameOverride);
        }

        if (!string.IsNullOrWhiteSpace(configuredHost))
        {
            return NormalizeHost(configuredHost);
        }

        return DefaultHost;
    }

    public string GetApiBaseUrl(string host) => NormalizeHost(host) + "/api";
}
