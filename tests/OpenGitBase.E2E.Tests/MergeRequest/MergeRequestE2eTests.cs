using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.MergeRequest;

[Collection("Compose")]
[Trait("Category", "MergeRequest")]
[Trait("RequiresCompose", "true")]
[E2eTier(6)]
public class MergeRequestE2eTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task ProtectBranchPushDeniedAndCreateMergeRequest()
    {
        BeginScenario();
        var owner = $"mr-owner-{Context.RunSuffix}";
        var writer = $"mr-writer-{Context.RunSuffix}";
        var password = "Password123!";
        var repoSlug = $"mr-e2e-{Context.RunSuffix}";
        var gitOps = new GitOperations(Transcript);
        var workRoot = Path.Combine(Path.GetTempPath(), $"e2e-mr-{Context.RunSuffix}");

        try
        {
            var ownerClient = await RegisterVerifiedUserAsync(owner, password).ConfigureAwait(false);
            var writerClient = await RegisterVerifiedUserAsync(writer, password).ConfigureAwait(false);
            var writerLogin = await writerClient.PostAsync("/signin/login", new { username = writer, password }).ConfigureAwait(false);
            var writerId = E2eScenarioHelpers.ExtractUserIdFromJwt(E2eScenarioHelpers.ParseJwtToken(writerLogin));

            Transcript.Describe("Create repository and add writer member");
            var repoCreate = await ownerClient.PostAsync($"/repository/{repoSlug}", new
            {
                repositoryName = "Merge Request E2E",
                isPrivate = false,
            }).ConfigureAwait(false);
            Assert.Equal(201, repoCreate.StatusCode);
            var repoId = E2eScenarioHelpers.ParseRepositoryId(repoCreate);
            await ownerClient.PostAsync("/repository-member", new
            {
                modelToCreate = new
                {
                    repositoryId = new { value = repoId },
                    userId = new { value = writerId },
                    role = 2,
                },
            }).ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            Transcript.Describe("Push main and feature branch via git");
            var ownerPat = E2eScenarioHelpers.ParsePatToken(await ownerClient.PostAsync("/git-access-token", new { name = "mr-e2e", scope = "write" }).ConfigureAwait(false));
            var remote = E2eEnvironment.BuildPatRemoteUrl(owner, repoSlug, ownerPat);
            var workDir = Path.Combine(workRoot, "work");
            await gitOps.InitRepositoryAsync(workDir, "main", owner, $"{owner}@example.com").ConfigureAwait(false);
            await gitOps.CommitFileAsync(workDir, "README.md", "initial\n", "initial commit").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            await gitOps.PushAsync(workDir, "origin", "main").ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);
            await gitOps.CheckoutBranchAsync(workDir, "feature/mr-e2e", create: true).ConfigureAwait(false);
            await gitOps.CommitFileAsync(workDir, "README.md", "change\n", "feature commit", append: true).ConfigureAwait(false);
            await gitOps.PushAsync(workDir, "origin", "feature/mr-e2e").ConfigureAwait(false);

            var mergeRequestBase = $"/repository/by-slug/{owner}/{repoSlug}/merge-requests";

            Transcript.Describe("Create merge request");
            var createMr = await ownerClient.PostAsync(mergeRequestBase, new
            {
                title = "E2E merge request",
                body = "Related work for branch protection",
                sourceRef = "feature/mr-e2e",
                targetRef = "main",
                isDraft = false,
            }).ConfigureAwait(false);
            Assert.True(createMr.StatusCode is 200 or 201, createMr.Body);
            await Baselines.CaptureApiAsync("create-mr", createMr).ConfigureAwait(false);

            Transcript.Describe("Protect main branch and deny writer direct push");
            var protect = await ownerClient.PostAsync($"/repository/{repoId}/protected-branch-rules", new
            {
                pattern = "main",
                blockDirectPush = true,
                allowedPushRoles = 2,
                requiredApprovalCount = 0,
                mergeRoleThreshold = 2,
                forcePushPolicy = 0,
                pushRules = new[] { new { ruleType = 3, configJson = "{\"required\":true}" } },
            }).ConfigureAwait(false);
            Assert.Equal(201, protect.StatusCode);

            await gitOps.CheckoutBranchAsync(workDir, "main", create: false).ConfigureAwait(false);
            await gitOps.CommitFileAsync(workDir, "README.md", "blocked-direct\n", "direct change on main", append: true).ConfigureAwait(false);
            var writerPat = E2eScenarioHelpers.ParsePatToken(await writerClient.PostAsync("/git-access-token", new { name = "writer", scope = "write" }).ConfigureAwait(false));
            var writerRemote = E2eEnvironment.BuildPatRemoteUrl(owner, repoSlug, writerPat);
            await gitOps.SetRemoteUrlAsync(workDir, "origin", writerRemote).ConfigureAwait(false);
            var pushResult = await gitOps.TryPushAsync(workDir, "origin", "main").ConfigureAwait(false);
            Assert.False(pushResult.Succeeded);
            await Baselines.CaptureApiAsync("protected-push-denied", new HttpCapture
            {
                StatusCode = pushResult.ExitCode,
                Body = pushResult.StdErr,
                Method = "GIT",
                Url = "push main",
            }).ConfigureAwait(false);
            await AssertBaselinesAsync().ConfigureAwait(false);
        }
        finally
        {
            if (Directory.Exists(workRoot))
            {
                Directory.Delete(workRoot, true);
            }
        }
    }

    private async Task<E2eApiClient> RegisterVerifiedUserAsync(string username, string password)
    {
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        await anon.PostAsync("/register/register", new { username, email = $"{username}@example.com", password }).ConfigureAwait(false);
        var login = await anon.PostAsync("/signin/login", new { username, password }).ConfigureAwait(false);
        var client = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(login));
        await client.PostAsync("/account/debug/verify-email", null).ConfigureAwait(false);
        return client;
    }
}
