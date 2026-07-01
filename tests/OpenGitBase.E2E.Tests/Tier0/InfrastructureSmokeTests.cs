using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Tier0;

[Collection("Compose")]
[Trait("Category", "Tier0")]
[Trait("RequiresCompose", "true")]
[E2eTier(0)]
public class InfrastructureSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task ApiHealthReturnsHealthy()
    {
        BeginScenario();
        Transcript.Describe("Verify compose stack health endpoint");
        using var client = new HttpClient();
        var response = await client.GetAsync(E2eEnvironment.ApiHealthUrl).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        await Baselines.CaptureApiAsync("health", new HttpCapture
        {
            StatusCode = (int)response.StatusCode,
            Body = body,
            Method = "GET",
            Url = E2eEnvironment.ApiHealthUrl.ToString(),
        }).ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode, body);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }
}
