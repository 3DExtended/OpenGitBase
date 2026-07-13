using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;
using OpenGitBase.Cli.Tests.TestSupport;
using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Cli.Integration.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class IssueCommandsIntegrationTests : ControllerTestBase
{
    public IssueCommandsIntegrationTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Issue_lifecycle_against_in_process_api()
    {
        var username = "cli-int-" + Guid.NewGuid().ToString("N")[..8];
        var token = await RegisterUserAsync(username, $"{username}@example.com").ConfigureAwait(false);
        await MarkEmailVerifiedAsync(username).ConfigureAwait(false);
        var slug = "cli-repo-" + Guid.NewGuid().ToString("N")[..8];

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRepo = await Client.PostAsJsonAsync(
            $"/repository/{slug}",
            new CreateRepositoryRequest("CLI Integration Repo", false)).ConfigureAwait(false);
        createRepo.EnsureSuccessStatusCode();

        var host = Client.BaseAddress!.ToString().TrimEnd('/');
        var configDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var overrides = BuildOverrides(host, token, configDir.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var createExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{username}/{slug}", "create", "--title", "CLI issue", "--body", "seed"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.True(createExit == 0, error.ToString());
        Assert.Contains("#1", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        var listExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{username}/{slug}", "list"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, listExit);
        Assert.Contains("CLI issue", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        var commentExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{username}/{slug}", "comment", "1", "--body", "follow-up"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, commentExit);

        output.GetStringBuilder().Clear();
        var statusExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{username}/{slug}", "status", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, statusExit);
        Assert.True(
            output.ToString().Contains("Open", StringComparison.Ordinal)
            || output.ToString().Contains("Engaged", StringComparison.Ordinal),
            output.ToString());

        output.GetStringBuilder().Clear();
        var closeExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{username}/{slug}", "close", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, closeExit);
        Assert.Contains("Resolved", output.ToString(), StringComparison.Ordinal);

        Directory.Delete(configDir.FullName, recursive: true);
    }

    [Fact]
    public async Task Auth_login_stores_token_from_loopback()
    {
        var username = "cli-auth-" + Guid.NewGuid().ToString("N")[..8];
        var token = await RegisterUserAsync(username, $"{username}@example.com").ConfigureAwait(false);
        await MarkEmailVerifiedAsync(username).ConfigureAwait(false);

        var host = Client.BaseAddress!.ToString().TrimEnd('/');
        var configDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var credentialStore = new InMemoryCredentialStore();
        var fakeLoopback = new FakeLoopbackAuthServer { TokenToReturn = token };
        var browser = new RecordingBrowserLauncher();

        using var output = new StringWriter();
        using var error = new StringWriter();
        var overrides = new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            ConfigStore = new FileConfigStore(Path.Combine(configDir.FullName, "hosts.yml")),
            LoopbackAuthServer = fakeLoopback,
            BrowserLauncher = browser,
            HttpClient = Factory.CreateClient(),
        };

        var exit = await CliApp.RunAsync(["--hostname", host, "auth", "login"], output, error, overrides)
            .ConfigureAwait(false);

        Assert.Equal(0, exit);
        Assert.Equal(token, credentialStore.GetToken(host));
        Assert.Contains("/cli/auth", browser.LastUrl!, StringComparison.Ordinal);
        Assert.Contains("port=54321", browser.LastUrl!, StringComparison.Ordinal);

        Directory.Delete(configDir.FullName, recursive: true);
    }

    private CliServiceOverrides BuildOverrides(string host, string token, string configDir)
    {
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.SaveToken(host, token);
        var cliClient = Factory.CreateClient();
        cliClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            ConfigStore = new FileConfigStore(Path.Combine(configDir, "hosts.yml")),
            HttpClient = cliClient,
            ApiClient = new OgbApiClient(cliClient, credentialStore, host, host),
        };
    }
}
