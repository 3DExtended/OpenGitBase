using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.HaChaos;

[Collection("Compose")]
[Trait("Category", "HaChaos")]
[Trait("Tag", "FullHa")]
[Trait("RequiresCompose", "true")]
[E2eTier(7)]
public class HaStorageChaosTests : E2eTestBase
{
    [RequiresFullHaFact]
    public async Task StopStorageNodeStillAllowsHealthCheck()
    {
        BeginScenario();
        var chaos = new ClusterChaos(Transcript);
        Transcript.Describe("Stop storage-2 and verify API health remains");
        await chaos.StopServiceAsync("storage-2").ConfigureAwait(false);
        try
        {
            using var client = new HttpClient();
            var health = await client.GetAsync(E2eEnvironment.ApiHealthUrl).ConfigureAwait(false);
            await Baselines.CaptureApiAsync("health-during-chaos", new HttpCapture
            {
                StatusCode = (int)health.StatusCode,
                Body = await health.Content.ReadAsStringAsync().ConfigureAwait(false),
                Method = "GET",
                Url = E2eEnvironment.ApiHealthUrl.ToString(),
            }).ConfigureAwait(false);
            Assert.True(health.IsSuccessStatusCode);
        }
        finally
        {
            await chaos.RestoreAllAsync().ConfigureAwait(false);
        }

        await AssertBaselinesAsync().ConfigureAwait(false);
    }
}
