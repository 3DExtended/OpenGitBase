using System.Text.Json;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Tier0;

[Collection("Compose")]
[Trait("Category", "Tier0")]
[Trait("RequiresCompose", "true")]
[E2eTier(0)]
public class PublicStatusSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task PublicStatusReturnsSnapshotShape()
    {
        BeginScenario();
        Transcript.Describe("Verify anonymous public status endpoint");
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        var response = await client.GetAsync("public/status").ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.True(response.IsSuccessStatusCode, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("overallStatus", out _));
        Assert.True(root.TryGetProperty("checkedAt", out _));
        Assert.True(root.TryGetProperty("groups", out var groups));
        Assert.Equal(JsonValueKind.Array, groups.ValueKind);
    }

    [RequiresComposeFact]
    public async Task PublicStatusHistoryReturnsSeriesShape()
    {
        BeginScenario();
        Transcript.Describe("Verify anonymous public status history endpoint");
        using var client = new HttpClient { BaseAddress = E2eEnvironment.ApiBaseUrl };
        var response = await client.GetAsync("public/status/history?days=7").ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        Assert.True(response.IsSuccessStatusCode, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("overall", out _));
        Assert.True(root.TryGetProperty("groups", out _));
        Assert.True(root.TryGetProperty("overallStateMix", out _));
    }
}
