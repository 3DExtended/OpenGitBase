using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Discovery;

[Collection("Compose")]
[Trait("Category", "Discovery")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(4)]
public class DiscoverySmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task PublicRepositoriesListReturnsOk()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var response = await anon.GetAsync("/public/repositories").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("public-repositories", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PublicRecentRepositoriesReturnsOk()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var response = await anon.GetAsync("/public/repositories/recent").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("public-recent-repositories", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PublicOwnerProfileReturnsOk()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("owner-profile").ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var response = await anon.GetAsync($"/public/owners/{setup.Owner.Username}").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("public-owner-profile", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanListUnreadNotifications()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("owner-unread").ConfigureAwait(false);
        await TriggerNotificationAsync(setup).ConfigureAwait(false);
        var response = await setup.Owner.Client.GetAsync("/notifications?unreadOnly=true").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("owner-unread-notifications", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanListAllNotifications()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("owner-all").ConfigureAwait(false);
        await TriggerNotificationAsync(setup).ConfigureAwait(false);
        var response = await setup.Owner.Client.GetAsync("/notifications?unreadOnly=false").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("owner-all-notifications", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReaderCanListNotifications()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("reader-list").ConfigureAwait(false);
        var response = await setup.Reader.Client.GetAsync("/notifications?unreadOnly=true").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("reader-notifications", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task AnonymousCannotListNotifications()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var response = await anon.GetAsync("/notifications?unreadOnly=true").ConfigureAwait(false);
        Assert.Equal(401, response.StatusCode);
        await Baselines.CaptureApiAsync("anonymous-notifications-denied", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MarkUnknownNotificationReadReturnsNotFound()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("mark-read-missing").ConfigureAwait(false);
        var response = await setup.Owner.Client.PostAsync("/notifications/00000000-0000-0000-0000-000000000000/read", null).ConfigureAwait(false);
        Assert.Equal(404, response.StatusCode);
        await Baselines.CaptureApiAsync("mark-read-missing", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MarkNotificationReadReturnsNoContent()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("mark-read").ConfigureAwait(false);
        await TriggerNotificationAsync(setup).ConfigureAwait(false);
        var list = await setup.Owner.Client.GetAsync("/notifications?unreadOnly=true").ConfigureAwait(false);
        var notificationId = ParseFirstGuid(list.Body);
        var markRead = await setup.Owner.Client.PostAsync($"/notifications/{notificationId}/read", null).ConfigureAwait(false);
        Assert.Equal(204, markRead.StatusCode);
        await Baselines.CaptureApiAsync("mark-read", markRead).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PublicRepositoriesSearchResponds()
    {
        BeginScenario();
        var setup = await SeedNotificationScenarioAsync("search").ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var response = await anon.GetAsync($"/public/repositories?q={setup.Repository.Slug}").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("public-repository-search", response).ConfigureAwait(false);
    }

    private async Task<DiscoverySmokeScenario> SeedNotificationScenarioAsync(string prefix)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"discovery-smoke-owner-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"discovery-smoke-reader-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repositories.CreateAsync(owner, $"discovery-smoke-{prefix}-{Context.RunSuffix}", "Discovery Smoke", isPrivate: false)
            .ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);
        return new DiscoverySmokeScenario(owner, reader, repository);
    }

    private static async Task TriggerNotificationAsync(DiscoverySmokeScenario setup)
    {
        var createDiscussion = await setup.Owner.Client.PostAsync(
            $"/repository/by-slug/{setup.Owner.Username}/{setup.Repository.Slug}/discussions",
            new { title = "notify smoke", body = "seed" }).ConfigureAwait(false);
        var number = ParseInt(createDiscussion.Body, "number");
        await setup.Reader.Client.PostAsync(
            $"/repository/by-slug/{setup.Owner.Username}/{setup.Repository.Slug}/discussions/{number}/comments",
            new { bodyMarkdown = "notify owner" }).ConfigureAwait(false);
    }

    private static int ParseInt(string body, string propertyName)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty(propertyName).GetInt32();
    }

    private static Guid ParseFirstGuid(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            return Guid.Empty;
        }

        var idElement = first.GetProperty("id");
        return idElement.ValueKind == JsonValueKind.Object
            ? idElement.GetProperty("value").GetGuid()
            : idElement.GetGuid();
    }

    private sealed record DiscoverySmokeScenario(
        AuthenticatedClient Owner,
        AuthenticatedClient Reader,
        RepositorySeed Repository);
}
