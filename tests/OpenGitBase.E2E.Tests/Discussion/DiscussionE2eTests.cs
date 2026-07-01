using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Discussion;

[Collection("Compose")]
[Trait("Category", "Discussion")]
[Trait("RequiresCompose", "true")]
[E2eTier(4)]
public class DiscussionE2eTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task PublicAnonymousReadAndCreateForbidden()
    {
        BeginScenario();
        var seed = await SeedDiscussionUsersAsync().ConfigureAwait(false);
        var publicDisc = $"/repository/by-slug/{seed.Owner}/{seed.PublicRepo}/discussions";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);

        Transcript.Describe("Anonymous can list public discussions");
        var list = await anon.GetAsync(publicDisc).ConfigureAwait(false);
        Assert.Equal(200, list.StatusCode);
        await Baselines.CaptureApiAsync("anon-public-list", list).ConfigureAwait(false);

        Transcript.Describe("Anonymous cannot create discussion");
        var create = await anon.PostAsync(publicDisc, new { title = "anon", body = "nope" }).ConfigureAwait(false);
        Assert.Equal(401, create.StatusCode);
        await Baselines.CaptureApiAsync("anon-create-denied", create).ConfigureAwait(false);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PrivateRepoAccessMatrix()
    {
        BeginScenario();
        var seed = await SeedDiscussionUsersAsync().ConfigureAwait(false);
        var privateDisc = $"/repository/by-slug/{seed.Owner}/{seed.PrivateRepo}/discussions";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);

        Transcript.Describe("Anonymous private list returns 404");
        var anonList = await anon.GetAsync(privateDisc).ConfigureAwait(false);
        Assert.Equal(404, anonList.StatusCode);
        await Baselines.CaptureApiAsync("private-anon-404", anonList).ConfigureAwait(false);

        Transcript.Describe("Outsider private list returns 403 or 404");
        var outsiderList = await seed.OutsiderClient.GetAsync(privateDisc).ConfigureAwait(false);
        Assert.True(outsiderList.StatusCode is 403 or 404, outsiderList.Body);
        await Baselines.CaptureApiAsync("private-outsider-403", outsiderList).ConfigureAwait(false);

        Transcript.Describe("Member can list private discussions");
        var memberList = await seed.ReaderClient.GetAsync(privateDisc).ConfigureAwait(false);
        Assert.Equal(200, memberList.StatusCode);
        await Baselines.CaptureApiAsync("private-member-200", memberList).ConfigureAwait(false);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task DiscussionCommentLifecycle()
    {
        BeginScenario();
        var seed = await SeedDiscussionUsersAsync().ConfigureAwait(false);
        var publicDisc = $"/repository/by-slug/{seed.Owner}/{seed.PublicRepo}/discussions";

        Transcript.Describe("Owner creates discussion");
        var create = await seed.OwnerClient.PostAsync(publicDisc, new { title = "E2E discussion", body = "seed" }).ConfigureAwait(false);
        Assert.True(create.StatusCode is 200 or 201, create.Body);
        var number = E2eScenarioHelpers.ParseDiscussionNumber(create);

        Transcript.Describe("Reader comment engages discussion");
        var comment = await seed.ReaderClient.PostAsync($"{publicDisc}/{number}/comments", new { bodyMarkdown = "reader engages" }).ConfigureAwait(false);
        Assert.Equal(200, comment.StatusCode);
        await Baselines.CaptureApiAsync("reader-comment", comment).ConfigureAwait(false);

        Transcript.Describe("Owner resolves discussion");
        var resolve = await seed.OwnerClient.PostAsync($"{publicDisc}/{number}/resolve", null).ConfigureAwait(false);
        Assert.Equal(200, resolve.StatusCode);
        var detail = await seed.OwnerClient.GetAsync($"{publicDisc}/{number}").ConfigureAwait(false);
        Assert.Equal(200, detail.StatusCode);
        await Baselines.CaptureApiAsync("resolved-detail", detail).ConfigureAwait(false);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }

    private async Task<DiscussionSeed> SeedDiscussionUsersAsync()
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var suffix = Context.RunSuffix;
        var publicRepo = $"disc-public-{suffix}";
        var privateRepo = $"disc-private-{suffix}";

        var owner = await identity.RegisterUserAsync($"disc-owner-{suffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"disc-reader-{suffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"disc-outsider-{suffix}").ConfigureAwait(false);

        await repositories.CreateAsync(owner, publicRepo, "Disc Public", isPrivate: false).ConfigureAwait(false);
        var privateSeed = await repositories.CreateAsync(owner, privateRepo, "Disc Private", isPrivate: true)
            .ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, privateSeed.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);

        return new DiscussionSeed(
            owner.Username,
            publicRepo,
            privateRepo,
            owner.Client,
            reader.Client,
            outsider.Client);
    }

    private sealed record DiscussionSeed(
        string Owner,
        string PublicRepo,
        string PrivateRepo,
        E2eApiClient OwnerClient,
        E2eApiClient ReaderClient,
        E2eApiClient OutsiderClient);
}
