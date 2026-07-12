using System.Net;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;
using OpenGitBase.Cli.Tests.TestSupport;

namespace OpenGitBase.Cli.Tests;

public sealed class AuthCommandTests
{
    [Fact]
    public async Task Auth_status_reports_logged_out()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = new CliServiceOverrides
        {
            CredentialStore = new InMemoryCredentialStore(),
            ConfigStore = new FileConfigStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hosts.yml")),
        };

        var exitCode = await CliApp.RunAsync(["auth", "status"], output, error, overrides);

        Assert.Equal(0, exitCode);
        Assert.Contains("Not logged in.", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auth_status_reports_logged_in_username()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var credentialStore = new InMemoryCredentialStore();
        const string host = "https://forge.example.com";
        credentialStore.SaveToken(host, AuthCommandTestsHelpers.CreateJwt("alice"));

        var configPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hosts.yml");
        var configStore = new FileConfigStore(configPath);
        configStore.Save(new OgbConfigFile { ActiveHost = host });

        var overrides = new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            ConfigStore = configStore,
        };

        var exitCode = await CliApp
            .RunAsync(["--hostname", host, "auth", "status"], output, error, overrides);

        Assert.Equal(0, exitCode);
        Assert.Contains("alice", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auth_logout_clears_credentials()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var credentialStore = new InMemoryCredentialStore();
        const string host = "https://forge.example.com";
        credentialStore.SaveToken(host, AuthCommandTestsHelpers.CreateJwt("alice"));

        var configPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hosts.yml");
        var configStore = new FileConfigStore(configPath);
        configStore.Save(new OgbConfigFile { ActiveHost = host, LoggedInUsername = "alice" });

        var overrides = new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            ConfigStore = configStore,
        };

        var exitCode = await CliApp
            .RunAsync(["--hostname", host, "auth", "logout"], output, error, overrides);

        Assert.Equal(0, exitCode);
        Assert.False(credentialStore.HasToken(host));
        Assert.Null(configStore.Load().ActiveHost);
    }

    [Fact]
    public async Task Auth_login_stores_token_from_loopback_callback()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var credentialStore = new InMemoryCredentialStore();
        var configPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hosts.yml");
        var configStore = new FileConfigStore(configPath);
        var browser = new RecordingBrowserLauncher();
        var loopback = new FakeLoopbackAuthServer
        {
            TokenToReturn = AuthCommandTestsHelpers.CreateJwt("alice"),
        };

        var overrides = new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            ConfigStore = configStore,
            LoopbackAuthServer = loopback,
            BrowserLauncher = browser,
        };

        var exitCode = await CliApp.RunAsync(
            ["--hostname", "https://forge.example.com", "auth", "login"],
            output,
            error,
            overrides);

        Assert.Equal(0, exitCode);
        Assert.True(credentialStore.HasToken("https://forge.example.com"));
        Assert.Contains("/cli/auth?port=54321", browser.LastUrl, StringComparison.Ordinal);
    }
}
