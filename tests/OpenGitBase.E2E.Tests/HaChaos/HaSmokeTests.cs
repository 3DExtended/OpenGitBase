using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.HaChaos;

[Collection("Compose")]
[Trait("Category", "HaChaos")]
[Trait("Tag", "Smoke")]
[Trait("Tag", "FullHa")]
[Trait("RequiresCompose", "true")]
[E2eTier(7)]
public class HaSmokeTests : E2eTestBase
{
    [RequiresFullHaFact]
    public async Task StopStorageOneHealthStillSucceeds()
    {
        BeginScenario();
        var chaos = new ClusterChaos(Transcript);

        Transcript.Describe("Stop storage-1 and verify health endpoint remains available");
        await chaos.StopServiceAsync("storage-1").ConfigureAwait(false);
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(E2eEnvironment.ApiHealthUrl).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.True(response.IsSuccessStatusCode, body);
            await Baselines.CaptureApiAsync("health-storage-1-stopped", new HttpCapture
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Method = "GET",
                Url = E2eEnvironment.ApiHealthUrl.ToString(),
            }).ConfigureAwait(false);
        }
        finally
        {
            await chaos.RestoreAllAsync().ConfigureAwait(false);
        }
    }

    [RequiresFullHaFact]
    public async Task StopStorageTwoHealthStillSucceeds()
    {
        BeginScenario();
        var chaos = new ClusterChaos(Transcript);

        Transcript.Describe("Stop storage-2 and verify health endpoint remains available");
        await chaos.StopServiceAsync("storage-2").ConfigureAwait(false);
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(E2eEnvironment.ApiHealthUrl).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.True(response.IsSuccessStatusCode, body);
            await Baselines.CaptureApiAsync("health-storage-2-stopped", new HttpCapture
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Method = "GET",
                Url = E2eEnvironment.ApiHealthUrl.ToString(),
            }).ConfigureAwait(false);
        }
        finally
        {
            await chaos.RestoreAllAsync().ConfigureAwait(false);
        }
    }

    [RequiresFullHaFact]
    public async Task StorageNodeDownStillAllowsRepositoryBrowse()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var owner = await identity.RegisterUserAsync($"ha-browse-owner-{Context.RunSuffix}").ConfigureAwait(false);
        var repoSlug = $"ha-browse-{Context.RunSuffix}";
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        await repositories.CreateAsync(owner, repoSlug, "HA browse repo", isPrivate: false).ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var chaos = new ClusterChaos(Transcript);

        Transcript.Describe("Browse refs while storage-3 is stopped");
        await chaos.StopServiceAsync("storage-3").ConfigureAwait(false);
        try
        {
            var refs = await anon.GetAsync($"/repository/by-slug/{owner.Username}/{repoSlug}/content/refs").ConfigureAwait(false);
            Assert.Equal(200, refs.StatusCode);
            await Baselines.CaptureApiAsync("browse-while-storage-3-stopped", refs).ConfigureAwait(false);
        }
        finally
        {
            await chaos.RestoreAllAsync().ConfigureAwait(false);
        }
    }
}
