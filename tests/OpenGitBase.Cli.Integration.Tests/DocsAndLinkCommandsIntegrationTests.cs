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
public sealed class DocsAndLinkCommandsIntegrationTests : ControllerTestBase
{
    public DocsAndLinkCommandsIntegrationTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Issue_link_and_docs_pull_against_in_process_api()
    {
        var username = "cli-link-" + Guid.NewGuid().ToString("N")[..8];
        var token = await RegisterUserAsync(username, $"{username}@example.com").ConfigureAwait(false);
        await MarkEmailVerifiedAsync(username).ConfigureAwait(false);
        var slug = "cli-link-repo-" + Guid.NewGuid().ToString("N")[..8];

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRepo = await Client.PostAsJsonAsync(
            $"/repository/{slug}",
            new CreateRepositoryRequest("CLI Link Repo", false)).ConfigureAwait(false);
        createRepo.EnsureSuccessStatusCode();

        var host = Client.BaseAddress!.ToString().TrimEnd('/');
        var configDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var outputDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var overrides = BuildOverrides(host, token, configDir.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var prdExit = await CliApp.RunAsync(
            [
                "--hostname", host,
                "issue", "-R", $"{username}/{slug}",
                "create", "--title", "[PRD] CLI docs pull", "--body", "Spec body",
            ],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, prdExit);

        output.GetStringBuilder().Clear();
        var sliceExit = await CliApp.RunAsync(
            [
                "--hostname", host,
                "issue", "-R", $"{username}/{slug}",
                "create", "--title", "[slice] cli-01 — Link test", "--body", "Slice body",
            ],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, sliceExit);

        output.GetStringBuilder().Clear();
        var linkExit = await CliApp.RunAsync(
            [
                "--hostname", host,
                "issue", "-R", $"{username}/{slug}",
                "link", "2", "--parent", "1",
            ],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, linkExit);
        Assert.Contains("parent", output.ToString(), StringComparison.OrdinalIgnoreCase);

        output.GetStringBuilder().Clear();
        var linksExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{username}/{slug}", "links", "2"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, linksExit);
        Assert.Contains("#1", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        var pullExit = await CliApp.RunAsync(
            [
                "--hostname", host,
                "docs", "-R", $"{username}/{slug}",
                "pull", "--output-dir", outputDir.FullName,
            ],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, pullExit);

        var prdPath = Path.Combine(outputDir.FullName, "docs", "prd", "cli-docs-pull.md");
        var slicePath = Path.Combine(outputDir.FullName, "docs", "issues", "cli-01.md");
        Assert.True(File.Exists(prdPath));
        Assert.True(File.Exists(slicePath));
        Assert.Contains("<!-- forge: #1 -->", await File.ReadAllTextAsync(prdPath).ConfigureAwait(false), StringComparison.Ordinal);

        Directory.Delete(configDir.FullName, recursive: true);
        Directory.Delete(outputDir.FullName, recursive: true);
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
