using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "Repository")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class BrowseE2eTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task PublicRefsAndTreeAnonymousAccess()
    {
        BeginScenario();
        var owner = $"browse-owner-{Context.RunSuffix}";
        var password = "Password123!";
        var publicSlug = $"browse-public-{Context.RunSuffix}";
        var privateSlug = $"browse-private-{Context.RunSuffix}";
        var gitOps = new GitOperations(Transcript);
        var workDir = Path.Combine(Path.GetTempPath(), $"e2e-browse-{Context.RunSuffix}");

        try
        {
            var ownerClient = await RegisterVerifiedUserAsync(owner, password).ConfigureAwait(false);
            await ownerClient.PostAsync($"/repository/{publicSlug}", new { repositoryName = "Browse Public", isPrivate = false }).ConfigureAwait(false);
            await ownerClient.PostAsync($"/repository/{privateSlug}", new { repositoryName = "Browse Private", isPrivate = true }).ConfigureAwait(false);
            await E2eScenarioHelpers.WaitForStorageProvisioningAsync().ConfigureAwait(false);

            var patResponse = await ownerClient.PostAsync("/git-access-token", new { name = "browse", scope = "write" }).ConfigureAwait(false);
            Assert.Equal(201, patResponse.StatusCode);
            var pat = E2eScenarioHelpers.ParsePatToken(patResponse);
            var remote = E2eEnvironment.BuildPatRemoteUrl(owner, publicSlug, pat);
            await gitOps.InitRepositoryAsync(workDir, "main", owner, $"{owner}@example.com").ConfigureAwait(false);
            await gitOps.CommitFileAsync(workDir, "README.md", "browse-me\n", "initial").ConfigureAwait(false);
            await gitOps.AddRemoteAsync(workDir, "origin", remote).ConfigureAwait(false);
            await gitOps.PushAsync(workDir, "origin", "main").ConfigureAwait(false);

            var anon = new E2eApiClient(Transcript, Context.Normalizer);
            var refsUrl = $"/repository/by-slug/{owner}/{publicSlug}/content/refs";
            Transcript.Describe("Anonymous public refs returns 200");
            var refs = await anon.GetAsync(refsUrl).ConfigureAwait(false);
            Assert.Equal(200, refs.StatusCode);
            await Baselines.CaptureApiAsync("public-refs", refs).ConfigureAwait(false);

            Transcript.Describe("Anonymous public tree returns 200");
            var tree = await anon.GetAsync($"/repository/by-slug/{owner}/{publicSlug}/content/tree?refName=main&path=").ConfigureAwait(false);
            Assert.Equal(200, tree.StatusCode);
            await Baselines.CaptureApiAsync("public-tree", tree).ConfigureAwait(false);

            Transcript.Describe("Anonymous private refs returns 404");
            var privateRefs = await anon.GetAsync($"/repository/by-slug/{owner}/{privateSlug}/content/refs").ConfigureAwait(false);
            Assert.Equal(404, privateRefs.StatusCode);
            await Baselines.CaptureApiAsync("private-anon-404", privateRefs).ConfigureAwait(false);
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
