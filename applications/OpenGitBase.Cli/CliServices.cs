using System.CommandLine;
using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;
using OpenGitBase.Cli.Git;
using OpenGitBase.Cli.Output;

namespace OpenGitBase.Cli;

public sealed class CliServices
{
    public CliServices(
        IHostResolver hostResolver,
        IConfigStore configStore,
        ICredentialStore credentialStore,
        ILoopbackAuthServer loopbackAuthServer,
        IBrowserLauncher browserLauncher,
        IGitRemoteResolver gitRemoteResolver,
        IGitBranchResolver gitBranchResolver,
        IOgbApiClient apiClient,
        string host,
        string apiBaseUrl,
        IOutputWriter outputWriter,
        bool jsonOutput,
        TextWriter output,
        TextWriter error,
        string? repoOverride,
        string? workingDirectory)
    {
        HostResolver = hostResolver;
        ConfigStore = configStore;
        CredentialStore = credentialStore;
        LoopbackAuthServer = loopbackAuthServer;
        BrowserLauncher = browserLauncher;
        GitRemoteResolver = gitRemoteResolver;
        GitBranchResolver = gitBranchResolver;
        ApiClient = apiClient;
        Host = host;
        ApiBaseUrl = apiBaseUrl;
        OutputWriter = outputWriter;
        JsonOutput = jsonOutput;
        Output = output;
        Error = error;
        RepoOverride = repoOverride;
        WorkingDirectory = workingDirectory;
    }

    public IHostResolver HostResolver { get; }

    public IConfigStore ConfigStore { get; }

    public ICredentialStore CredentialStore { get; }

    public ILoopbackAuthServer LoopbackAuthServer { get; }

    public IBrowserLauncher BrowserLauncher { get; }

    public IGitRemoteResolver GitRemoteResolver { get; }

    public IGitBranchResolver GitBranchResolver { get; }

    public IOgbApiClient ApiClient { get; }

    public string Host { get; }

    public string ApiBaseUrl { get; }

    public IOutputWriter OutputWriter { get; }

    public bool JsonOutput { get; }

    public TextWriter Output { get; }

    public TextWriter Error { get; }

    public string? RepoOverride { get; }

    public string? WorkingDirectory { get; }

    public static CliServices CreateDefault(
        ParseResult parseResult,
        TextWriter output,
        TextWriter error,
        CliServiceOverrides? overrides = null)
    {
        overrides ??= new CliServiceOverrides();
        var hostResolver = overrides.HostResolver ?? new HostResolver();
        var configStore = overrides.ConfigStore ?? new FileConfigStore();
        var credentialStore = overrides.CredentialStore ?? CredentialStoreFactory.CreateDefault(hostResolver);
        var config = configStore.Load();
        var hostnameOverride = parseResult.GetValue(CliOptions.HostnameOption);
        var host = hostResolver.ResolveHost(hostnameOverride, config.ActiveHost);
        var apiBaseUrl = hostResolver.GetApiBaseUrl(host);
        var jsonOutput = parseResult.GetValue(CliOptions.JsonOption);
        var outputWriter = OutputWriterFactory.Create(jsonOutput, output, error);
        var httpClient = overrides.HttpClient ?? new HttpClient();
        var apiClient = overrides.ApiClient ?? new OgbApiClient(httpClient, credentialStore, host, apiBaseUrl);
        var loopbackAuthServer = overrides.LoopbackAuthServer ?? new LoopbackAuthServer();
        var browserLauncher = overrides.BrowserLauncher ?? new SystemBrowserLauncher();
        var gitRemoteResolver = overrides.GitRemoteResolver ?? new GitRemoteResolver();
        var gitBranchResolver = overrides.GitBranchResolver ?? new GitBranchResolver();
        var repoOverride = parseResult.GetValue(CliOptions.RepoOption);

        return new CliServices(
            hostResolver,
            configStore,
            credentialStore,
            loopbackAuthServer,
            browserLauncher,
            gitRemoteResolver,
            gitBranchResolver,
            apiClient,
            host,
            apiBaseUrl,
            outputWriter,
            jsonOutput,
            output,
            error,
            repoOverride,
            overrides.WorkingDirectory);
    }
}