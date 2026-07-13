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
public sealed class CliMrE2eTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task Cli_mr_create_list_view_and_close_against_compose()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var mergeFixture = new MergeRequestFixture(Transcript, Context.Normalizer);
        var suffix = Context.RunSuffix;
        var owner = await identity.RegisterUserAsync($"cli-mr-owner-{suffix}").ConfigureAwait(false);
        var writer = await identity.RegisterUserAsync($"cli-mr-writer-{suffix}").ConfigureAwait(false);
        var workRoot = Path.Combine(Path.GetTempPath(), $"ogb-mr-e2e-{suffix}");
        var seed = await mergeFixture
            .SeedMrReadyAsync(owner, writer, $"cli-mr-{suffix}", workRoot)
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
                Path.Combine(Path.GetTempPath(), $"ogb-mr-e2e-{suffix}", "hosts.yml")),
        };

        Transcript.Describe("CLI creates merge request");
        var createExit = await CliApp.RunAsync(
            [
                "--hostname", host,
                "mr", "-R", $"{owner.Username}/{seed.Repository.Slug}",
                "create", "--title", "CLI MR E2E", "--head", seed.FeatureBranch, "--base", "main",
            ],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, createExit);
        Assert.Contains("#1", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Transcript.Describe("CLI lists merge requests");
        var listExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{owner.Username}/{seed.Repository.Slug}", "list"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, listExit);
        Assert.Contains("CLI MR E2E", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Transcript.Describe("CLI views merge request");
        var viewExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{owner.Username}/{seed.Repository.Slug}", "view", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, viewExit);
        Assert.Contains(seed.FeatureBranch, output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Transcript.Describe("CLI closes merge request");
        var closeExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{owner.Username}/{seed.Repository.Slug}", "close", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, closeExit);
        Assert.Contains("Closed", output.ToString(), StringComparison.Ordinal);
    }
}
