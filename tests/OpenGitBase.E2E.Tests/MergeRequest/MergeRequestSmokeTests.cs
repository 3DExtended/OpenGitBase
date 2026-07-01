using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.MergeRequest;

[Collection("Compose")]
[Trait("Category", "MergeRequest")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(6)]
public class MergeRequestSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task CreateMergeRequestReturnsCreated()
    {
        BeginScenario();
        var setup = await SeedScenarioAsync("create").ConfigureAwait(false);
        var create = await setup.Owner.Client.PostAsync(setup.BaseUrl, setup.CreateBody).ConfigureAwait(false);
        Assert.True(create.StatusCode is 200 or 201, create.Body);
        await Baselines.CaptureApiAsync("create-mr", create).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MergeRequestListIncludesCreatedEntry()
    {
        BeginScenario();
        var setup = await SeedScenarioAsync("list").ConfigureAwait(false);
        await setup.Owner.Client.PostAsync(setup.BaseUrl, setup.CreateBody).ConfigureAwait(false);
        var list = await setup.Owner.Client.GetAsync(setup.BaseUrl).ConfigureAwait(false);
        Assert.Equal(200, list.StatusCode);
        Assert.Contains("number", list.Body, StringComparison.OrdinalIgnoreCase);
        await Baselines.CaptureApiAsync("list-mr", list).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MergeRequestDetailIsReadable()
    {
        BeginScenario();
        var setup = await SeedScenarioAsync("detail").ConfigureAwait(false);
        var create = await setup.Owner.Client.PostAsync(setup.BaseUrl, setup.CreateBody).ConfigureAwait(false);
        var number = ParseInt(create.Body, "number");
        var detail = await setup.Owner.Client.GetAsync($"{setup.BaseUrl}/{number}").ConfigureAwait(false);
        Assert.Equal(200, detail.StatusCode);
        await Baselines.CaptureApiAsync("detail-mr", detail).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task WriterCannotApproveOwnerMergeRequest()
    {
        BeginScenario();
        var setup = await SeedScenarioAsync("approve").ConfigureAwait(false);
        var create = await setup.Owner.Client.PostAsync(setup.BaseUrl, setup.CreateBody).ConfigureAwait(false);
        var number = ParseInt(create.Body, "number");
        var approve = await setup.Writer.Client.PostAsync($"{setup.BaseUrl}/{number}/approve", null).ConfigureAwait(false);
        Assert.Equal(400, approve.StatusCode);
        await Baselines.CaptureApiAsync("approve-mr-denied", approve).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MergeabilityEndpointResponds()
    {
        BeginScenario();
        var setup = await SeedScenarioAsync("mergeability").ConfigureAwait(false);
        var create = await setup.Owner.Client.PostAsync(setup.BaseUrl, setup.CreateBody).ConfigureAwait(false);
        var number = ParseInt(create.Body, "number");
        var mergeability = await setup.Owner.Client.GetAsync($"{setup.BaseUrl}/{number}/mergeability").ConfigureAwait(false);
        Assert.Equal(200, mergeability.StatusCode);
        await Baselines.CaptureApiAsync("mergeability", mergeability).ConfigureAwait(false);
    }

    private async Task<MergeRequestSmokeScenario> SeedScenarioAsync(string prefix)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var mergeFixture = new MergeRequestFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"mr-smoke-owner-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var writer = await identity.RegisterUserAsync($"mr-smoke-writer-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var seed = await mergeFixture.SeedMrReadyAsync(
            owner,
            writer,
            $"mr-smoke-{prefix}-{Context.RunSuffix}",
            Path.Combine(Path.GetTempPath(), $"e2e-mr-smoke-{prefix}-{Context.RunSuffix}"))
            .ConfigureAwait(false);

        return new MergeRequestSmokeScenario(
            owner,
            writer,
            seed.MergeRequestBase,
            new
            {
                title = "Smoke merge request",
                body = "smoke body",
                sourceRef = seed.FeatureBranch,
                targetRef = "main",
                isDraft = false,
            });
    }

    private static int ParseInt(string body, string propertyName)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty(propertyName).GetInt32();
    }

    private sealed record MergeRequestSmokeScenario(
        AuthenticatedClient Owner,
        AuthenticatedClient Writer,
        string BaseUrl,
        object CreateBody);
}
