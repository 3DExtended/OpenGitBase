using OpenGitBase.Cli.Configuration;

namespace OpenGitBase.Cli;

public sealed class CliRuntimeContext
{
    public CliRuntimeContext(IHostResolver hostResolver, IConfigStore configStore, string? hostnameOverride)
    {
        HostResolver = hostResolver;
        ConfigStore = configStore;
        var config = configStore.Load();
        Host = hostResolver.ResolveHost(hostnameOverride, config.ActiveHost);
        ApiBaseUrl = hostResolver.GetApiBaseUrl(Host);
    }

    public IHostResolver HostResolver { get; }

    public IConfigStore ConfigStore { get; }

    public string Host { get; }

    public string ApiBaseUrl { get; }
}
