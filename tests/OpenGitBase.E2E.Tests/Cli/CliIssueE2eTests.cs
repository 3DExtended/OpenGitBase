using OpenGitBase.Cli;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Cli;

[Collection("Compose")]
[Trait("Category", "Cli")]
[Trait("RequiresCompose", "true")]
[E2eTier(4)]
public sealed class CliIssueE2eTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task Cli_issue_create_list_and_close_against_compose()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var suffix = Context.RunSuffix;
        var owner = await identity.RegisterUserAsync($"cli-owner-{suffix}").ConfigureAwait(false);
        var repo = await repositories
            .CreateAsync(owner, $"cli-{suffix}", "CLI E2E Repo", isPrivate: false)
            .ConfigureAwait(false);

        var host = $"http://localhost:{E2eEnvironment.GitHttpPort}";
        var credentialStore = new InMemoryCredentialStore();
        credentialStore.SaveToken(host, owner.Token);

        using var httpClient = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        using var output = new StringWriter();
        using var error = new StringWriter();

        var overrides = new CliServiceOverrides
        {
            CredentialStore = credentialStore,
            HttpClient = httpClient,
            ConfigStore = new FileConfigStore(
                Path.Combine(Path.GetTempPath(), $"ogb-e2e-{suffix}", "hosts.yml")),
        };

        Transcript.Describe("CLI creates issue via Discussions API");
        var createExit = await CliApp.RunAsync(
            [
                "--hostname", host,
                "issue", "-R", $"{owner.Username}/{repo.Slug}",
                "create", "--title", "CLI E2E issue", "--body", "created from compose e2e",
            ],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, createExit);
        Assert.Contains("#1", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Transcript.Describe("CLI lists issues");
        var listExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{owner.Username}/{repo.Slug}", "list"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, listExit);
        Assert.Contains("CLI E2E issue", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Transcript.Describe("CLI closes issue");
        var closeExit = await CliApp.RunAsync(
            ["--hostname", host, "issue", "-R", $"{owner.Username}/{repo.Slug}", "close", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, closeExit);
        Assert.Contains("Resolved", output.ToString(), StringComparison.Ordinal);
    }
}
