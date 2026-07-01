using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.GitHttps;

[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class GitHttpsSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task InvalidPatScopeReturnsBadRequest()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"https-invalid-scope-{Context.RunSuffix}").ConfigureAwait(false);

        Transcript.Describe("Creating PAT with invalid scope returns 400");
        var create = await owner.Client.PostAsync("/git-access-token", new
        {
            name = "bad-scope",
            scope = "admin",
        }).ConfigureAwait(false);
        Assert.Equal(400, create.StatusCode);
        await Baselines.CaptureApiAsync("invalid-scope", create).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PatLifecycleCreateListAndDelete()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"https-pat-life-{Context.RunSuffix}").ConfigureAwait(false);

        Transcript.Describe("Create PAT, list it, then revoke it");
        var create = await owner.Client.PostAsync("/git-access-token", new
        {
            name = "lifecycle",
            scope = "read",
            neverExpires = true,
        }).ConfigureAwait(false);
        Assert.Equal(201, create.StatusCode);
        var patId = ParseGuid(create.Body, "id");

        var list = await owner.Client.GetAsync("/git-access-token").ConfigureAwait(false);
        Assert.Equal(200, list.StatusCode);
        Assert.Contains("lifecycle", list.Body, StringComparison.Ordinal);

        var delete = await owner.Client.SendAsync(HttpMethod.Delete, $"/git-access-token/{patId}").ConfigureAwait(false);
        Assert.Equal(204, delete.StatusCode);

        var get = await owner.Client.GetAsync($"/git-access-token/{patId}").ConfigureAwait(false);
        Assert.Equal(200, get.StatusCode);
        Assert.Contains("revokedAt", get.Body, StringComparison.Ordinal);
        await Baselines.CaptureApiAsync("pat-lifecycle", get).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task WritePatCanCloneSeededRepository()
    {
        BeginScenario();
        var setup = await SeedGitRepoAsync("push-write").ConfigureAwait(false);
        var git = new GitOperations(Transcript);

        Transcript.Describe("Write-scoped PAT remote is clonable after initial push");
        var cloneDir = Path.Combine(setup.WorkDir, "verify-write-clone");
        await git.CloneAsync(setup.WritePat.RemoteUrl, cloneDir).ConfigureAwait(false);
        new GitAssertions().AssertFileContains(cloneDir, "README.md", "initial");
        await Baselines.CaptureGitStateAsync("write-pat-clone", new GitAssertions().Inspect(cloneDir)).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReadPatCanCloneButCannotPush()
    {
        BeginScenario();
        var setup = await SeedGitRepoAsync("read-deny").ConfigureAwait(false);
        var git = new GitOperations(Transcript);
        var pats = new PatFixture(Transcript, Context.Normalizer);
        var readPat = await pats.CreateReadPatAsync(setup.Owner, setup.Owner.Username, setup.RepoSlug).ConfigureAwait(false);
        var readDir = Path.Combine(setup.WorkDir, "read-clone");

        Transcript.Describe("Read-scoped PAT can clone but push is denied");
        await git.CloneAsync(readPat.RemoteUrl, readDir).ConfigureAwait(false);
        await git.CommitFileAsync(readDir, "README.md", "read-denied\n", "should fail", append: true).ConfigureAwait(false);
        await git.SetRemoteUrlAsync(readDir, "origin", readPat.RemoteUrl).ConfigureAwait(false);
        var push = await git.TryPushAsync(readDir, "origin", "main").ConfigureAwait(false);
        Assert.False(push.Succeeded, push.StdErr);
        await Baselines.CaptureApiAsync("read-pat-push-denied", new HttpCapture
        {
            StatusCode = push.ExitCode,
            Body = push.StdErr,
            Method = "GIT",
            Url = "push",
        }).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task InvalidPatCloneFails()
    {
        BeginScenario();
        var setup = await SeedGitRepoAsync("invalid-clone").ConfigureAwait(false);
        var git = new GitOperations(Transcript);
        var cloneDir = Path.Combine(setup.WorkDir, "invalid-clone");
        var invalidRemote = E2eEnvironment.BuildPatRemoteUrl(setup.Owner.Username, setup.RepoSlug, "ogb_invalid_token");

        Transcript.Describe("Invalid PAT remote cannot clone repository");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => git.CloneAsync(invalidRemote, cloneDir)).ConfigureAwait(false);
        Assert.Contains("failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        await Baselines.CaptureApiAsync("invalid-pat-clone", new HttpCapture
        {
            StatusCode = 1,
            Body = ex.Message,
            Method = "GIT",
            Url = "clone",
        }).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task RevokedWritePatCannotPush()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"https-revoked-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var repoSlug = $"https-revoked-{Context.RunSuffix}";
        var repos = new RepositoryFixture(Transcript, Context.Normalizer);
        await repos.CreateAsync(owner, repoSlug, "Revoked PAT repo", isPrivate: false).ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
        var git = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-https-revoked-{Context.RunSuffix}");

        try
        {
            var create = await owner.Client.PostAsync("/git-access-token", new { name = "revoked-write", scope = "write" })
                .ConfigureAwait(false);
            Assert.Equal(201, create.StatusCode);
            var patId = ParseGuid(create.Body, "id");
            var token = E2eScenarioHelpers.ParsePatToken(create);
            var remote = E2eEnvironment.BuildPatRemoteUrl(owner.Username, repoSlug, token);

            await git.InitRepositoryAsync(workDir, "main", owner.Username, $"{owner.Username}@example.com").ConfigureAwait(false);
            await git.CommitFileAsync(workDir, "README.md", "initial\n", "initial").ConfigureAwait(false);
            await git.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            await git.PushAsync(workDir, "origin", "main").ConfigureAwait(false);

            var revoke = await owner.Client.SendAsync(HttpMethod.Delete, $"/git-access-token/{patId}").ConfigureAwait(false);
            Assert.Equal(204, revoke.StatusCode);

            await git.CommitFileAsync(workDir, "README.md", "revoked\n", "revoked push", append: true).ConfigureAwait(false);
            var push = await git.TryPushAsync(workDir, "origin", "main").ConfigureAwait(false);
            Assert.False(push.Succeeded, push.StdErr);
            await Baselines.CaptureApiAsync("revoked-pat-push-denied", new HttpCapture
            {
                StatusCode = push.ExitCode,
                Body = push.StdErr,
                Method = "GIT",
                Url = "push",
            }).ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }

    private async Task<GitHttpsSeed> SeedGitRepoAsync(string prefix)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"https-owner-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var repoSlug = $"https-repo-{prefix}-{Context.RunSuffix}";
        var repos = new RepositoryFixture(Transcript, Context.Normalizer);
        await repos.CreateAsync(owner, repoSlug, "HTTPS smoke repo", isPrivate: false).ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
        var pats = new PatFixture(Transcript, Context.Normalizer);
        var writePat = await pats.CreateWritePatAsync(owner, owner.Username, repoSlug).ConfigureAwait(false);
        var git = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-https-{prefix}-{Context.RunSuffix}");
        await git.InitRepositoryAsync(workDir, "main", owner.Username, $"{owner.Username}@example.com").ConfigureAwait(false);
        await git.CommitFileAsync(workDir, "README.md", "initial\n", "initial").ConfigureAwait(false);
        await git.AddRemoteAsync(workDir, "origin", writePat.RemoteUrl).ConfigureAwait(false);
        await git.PushAsync(workDir, "origin", "main").ConfigureAwait(false);
        await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
        return new GitHttpsSeed(owner, repoSlug, writePat, workDir);
    }

    private static Guid ParseGuid(string body, string property)
    {
        using var doc = JsonDocument.Parse(body);
        var idElement = doc.RootElement.GetProperty(property);
        return idElement.ValueKind == JsonValueKind.Object
            ? idElement.GetProperty("value").GetGuid()
            : idElement.GetGuid();
    }

    private sealed record GitHttpsSeed(
        AuthenticatedClient Owner,
        string RepoSlug,
        PatSeed WritePat,
        string WorkDir);
}
