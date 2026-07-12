namespace OpenGitBase.Cli.Configuration;

public interface IHostResolver
{
    string DefaultHost { get; }

    string NormalizeHost(string host);

    string ResolveHost(string? hostnameOverride, string? configuredHost);

    string GetApiBaseUrl(string host);
}
