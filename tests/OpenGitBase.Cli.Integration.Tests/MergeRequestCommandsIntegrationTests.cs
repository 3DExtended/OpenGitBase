using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenGitBase.Api;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;
using OpenGitBase.Cli;
using OpenGitBase.Cli.Api;
using OpenGitBase.Cli.Auth;
using OpenGitBase.Cli.Configuration;
using OpenGitBase.Cli.Tests.TestSupport;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Cli.Integration.Tests;

[Collection(ApiTestCollection.Name)]
public sealed class MergeRequestCommandsIntegrationTests : ControllerTestBase
{
    public MergeRequestCommandsIntegrationTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Mr_lifecycle_create_list_view_close_against_in_process_api()
    {
        var username = "cli-mr-" + Guid.NewGuid().ToString("N")[..8];
        var token = await RegisterUserAsync(username, $"{username}@example.com").ConfigureAwait(false);
        await MarkEmailVerifiedAsync(username).ConfigureAwait(false);
        var slug = "cli-mr-repo-" + Guid.NewGuid().ToString("N")[..8];

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createRepo = await Client.PostAsJsonAsync(
            $"/repository/{slug}",
            new CreateRepositoryRequest("CLI MR Integration Repo", false)).ConfigureAwait(false);
        createRepo.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>>();

        await using var context = await contextFactory.CreateDbContextAsync().ConfigureAwait(false);
        var user = await context
            .Set<OpenGitBase.Features.Users.Entities.UserCredentialsEntity>()
            .SingleAsync(x => x.Username == username)
            .ConfigureAwait(false);
        var repository = await queryProcessor
            .RunQueryAsync(
                new GetRepositoryByOwnerSlugQuery { OwnerSlug = username, Slug = slug },
                CancellationToken.None)
            .ConfigureAwait(false);
        Assert.False(repository.IsNone);

        var seeded = await queryProcessor
            .RunQueryAsync(
                new CreateMergeRequestQuery
                {
                    RepositoryId = repository.Get().Id.Value,
                    CreatorUserId = UserId.From(user.UserId),
                    Title = "CLI MR",
                    Body = "seeded for integration test",
                    SourceRef = "feature/cli",
                    TargetRef = "main",
                    SourceHeadSha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    TargetBaseSha = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                },
                CancellationToken.None)
            .ConfigureAwait(false);
        Assert.False(seeded.IsNone);

        var host = Client.BaseAddress!.ToString().TrimEnd('/');
        var configDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var overrides = BuildOverrides(host, token, configDir.FullName);

        using var output = new StringWriter();
        using var error = new StringWriter();

        var listExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{username}/{slug}", "list"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, listExit);
        Assert.Contains("CLI MR", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        var viewExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{username}/{slug}", "view", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, viewExit);
        Assert.Contains("feature/cli", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        var statusExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{username}/{slug}", "status", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, statusExit);
        Assert.Contains("Approved", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        var closeExit = await CliApp.RunAsync(
            ["--hostname", host, "mr", "-R", $"{username}/{slug}", "close", "1"],
            output,
            error,
            overrides).ConfigureAwait(false);
        Assert.Equal(0, closeExit);
        Assert.Contains("Closed", output.ToString(), StringComparison.Ordinal);

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
