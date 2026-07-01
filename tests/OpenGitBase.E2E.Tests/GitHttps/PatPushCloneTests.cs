using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.GitHttps;

[Collection("Compose")]
[Trait("Category", "GitHttps")]
[Trait("RequiresCompose", "true")]
[E2eTier(2)]
public class PatPushCloneTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task PatPushCloneAndReadScopeDenial()
    {
        BeginScenario();
        var username = $"https-git-e2e-{Context.RunSuffix}";
        var email = $"{username}@example.com";
        var password = "Password123!";
        var repoSlug = "https-e2e-repo";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var gitOps = new GitOperations(Transcript);
        var gitAssert = new GitAssertions();
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-git-{Context.RunSuffix}");

        try
        {
            Transcript.Describe("Register user and create repository via API");
            await anon.PostAsync("/register/register", new { username, email, password }).ConfigureAwait(false);
            var login = await anon.PostAsync("/signin/login", new { username, password }).ConfigureAwait(false);
            var jwt = E2eScenarioHelpers.ParseJwtToken(login);
            var client = new E2eApiClient(Transcript, Context.Normalizer, jwt);
            await client.PostAsync("/account/debug/verify-email", null).ConfigureAwait(false);

            var createRepo = await client.PostAsync($"/repository/{repoSlug}", new
            {
                repositoryName = "HTTPS E2E Repo",
                isPrivate = false,
            }).ConfigureAwait(false);
            Assert.Equal(201, createRepo.StatusCode);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            Transcript.Describe("Create write-scoped PAT");
            var writePatResponse = await client.PostAsync("/git-access-token", new
            {
                name = "e2e-write",
                scope = "write",
            }).ConfigureAwait(false);
            Assert.Equal(201, writePatResponse.StatusCode);
            var writePat = E2eScenarioHelpers.ParsePatToken(writePatResponse);
            Context.Normalizer.RegisterToken("WRITE_PAT", writePat);
            await Baselines.CaptureApiAsync("create-write-pat", writePatResponse).ConfigureAwait(false);

            var writeRemote = E2eEnvironment.BuildPatRemoteUrl(username, repoSlug, writePat);

            Transcript.Describe("Git init, commit, and push via HAProxy port 8089");
            await gitOps.InitRepositoryAsync(workDir, "main", username, email).ConfigureAwait(false);
            await gitOps.CommitFileAsync(workDir, "README.md", "hello-https\n", "initial").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", writeRemote).ConfigureAwait(false);
            await gitOps.PushAsync(workDir, "origin", "main").ConfigureAwait(false);

            Transcript.Describe("Clone repository and verify README content");
            var cloneDir = Path.Combine(workDir, "clone");
            await gitOps.CloneAsync(writeRemote, cloneDir).ConfigureAwait(false);
            gitAssert.AssertFileContains(cloneDir, "README.md", "hello-https");
            var gitState = gitAssert.Inspect(cloneDir);
            await Baselines.CaptureGitStateAsync("after-clone", gitState).ConfigureAwait(false);

            Transcript.Describe("Create read-scoped PAT and verify push is denied");
            var readPatResponse = await client.PostAsync("/git-access-token", new
            {
                name = "e2e-read",
                scope = "read",
            }).ConfigureAwait(false);
            Assert.Equal(201, readPatResponse.StatusCode);
            var readPat = E2eScenarioHelpers.ParsePatToken(readPatResponse);

            var readWorkDir = Path.Combine(workDir, "read-test");
            await gitOps.CloneAsync(writeRemote, readWorkDir).ConfigureAwait(false);
            await gitOps.CommitFileAsync(readWorkDir, "README.md", "blocked\n", "should fail", append: true).ConfigureAwait(false);
            var readRemote = E2eEnvironment.BuildPatRemoteUrl(username, repoSlug, readPat);
            await gitOps.SetRemoteUrlAsync(readWorkDir, "origin", readRemote).ConfigureAwait(false);
            var pushResult = await gitOps.TryPushAsync(readWorkDir, "origin", "main").ConfigureAwait(false);
            Assert.False(pushResult.Succeeded, pushResult.StdErr);
            await Baselines.CaptureApiAsync("read-pat-push-denied", new HttpCapture
            {
                StatusCode = pushResult.ExitCode,
                Body = pushResult.StdErr,
                Method = "GIT",
                Url = "push",
            }).ConfigureAwait(false);
            await AssertBaselinesAsync().ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, true);
            }
        }
    }
}
