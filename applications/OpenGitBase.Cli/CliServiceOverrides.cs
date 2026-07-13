namespace OpenGitBase.Cli;

public sealed class CliServiceOverrides
{
    public Configuration.IHostResolver? HostResolver { get; init; }

    public Configuration.IConfigStore? ConfigStore { get; init; }

    public Auth.ICredentialStore? CredentialStore { get; init; }

    public Auth.ILoopbackAuthServer? LoopbackAuthServer { get; init; }

    public Auth.IBrowserLauncher? BrowserLauncher { get; init; }

    public Git.IGitRemoteResolver? GitRemoteResolver { get; init; }

    public Git.IGitBranchResolver? GitBranchResolver { get; init; }

    public Api.IOgbApiClient? ApiClient { get; init; }

    public HttpClient? HttpClient { get; init; }

    public string? WorkingDirectory { get; init; }
}
