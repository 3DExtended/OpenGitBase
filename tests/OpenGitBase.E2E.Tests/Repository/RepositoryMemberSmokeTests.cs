using System.Text.Json;
using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "RepositoryMember")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class RepositoryMemberSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task OwnerCanListMembers()
    {
        BeginScenario();
        var setup = await SeedAsync("list-owner").ConfigureAwait(false);
        var list = await setup.Owner.Client.GetAsync($"/repository-member/{setup.Repository.RepositoryId}").ConfigureAwait(false);
        Assert.Equal(200, list.StatusCode);
        await Baselines.CaptureApiAsync("owner-list-members", list).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReaderCanListMembers()
    {
        BeginScenario();
        var setup = await SeedAsync("list-reader").ConfigureAwait(false);
        var list = await setup.Reader.Client.GetAsync($"/repository-member/{setup.Repository.RepositoryId}").ConfigureAwait(false);
        Assert.Equal(200, list.StatusCode);
        await Baselines.CaptureApiAsync("reader-list-members", list).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OutsiderCannotListMembers()
    {
        BeginScenario();
        var setup = await SeedAsync("list-outsider").ConfigureAwait(false);
        var list = await setup.Outsider.Client.GetAsync($"/repository-member/{setup.Repository.RepositoryId}").ConfigureAwait(false);
        Assert.Equal(403, list.StatusCode);
        await Baselines.CaptureApiAsync("outsider-list-members-denied", list).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanAddMember()
    {
        BeginScenario();
        var setup = await SeedAsync("add").ConfigureAwait(false);
        var add = await setup.Owner.Client.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        Assert.True(add.StatusCode is 200 or 201, add.Body);
        await Baselines.CaptureApiAsync("owner-add-member", add).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReaderCannotAddMember()
    {
        BeginScenario();
        var setup = await SeedAsync("reader-add-denied").ConfigureAwait(false);
        var add = await setup.Reader.Client.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        Assert.Equal(403, add.StatusCode);
        await Baselines.CaptureApiAsync("reader-add-member-denied", add).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanPromoteMemberRole()
    {
        BeginScenario();
        var setup = await SeedAsync("promote").ConfigureAwait(false);
        var add = await setup.Owner.Client.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        var memberId = ParseGuid(add.Body, "value");
        var update = await setup.Owner.Client.SendAsync(HttpMethod.Put, $"/repository-member/{memberId}", new
        {
            updatedModel = new
            {
                id = new { value = memberId },
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 2,
            },
        }).ConfigureAwait(false);
        Assert.Equal(204, update.StatusCode);
        await Baselines.CaptureApiAsync("owner-promote-member", update).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReaderCannotPromoteMemberRole()
    {
        BeginScenario();
        var setup = await SeedAsync("reader-promote-denied").ConfigureAwait(false);
        var add = await setup.Owner.Client.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        var memberId = ParseGuid(add.Body, "value");
        var update = await setup.Reader.Client.SendAsync(HttpMethod.Put, $"/repository-member/{memberId}", new
        {
            updatedModel = new
            {
                id = new { value = memberId },
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 2,
            },
        }).ConfigureAwait(false);
        Assert.Equal(403, update.StatusCode);
        await Baselines.CaptureApiAsync("reader-promote-denied", update).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanRemoveMember()
    {
        BeginScenario();
        var setup = await SeedAsync("remove").ConfigureAwait(false);
        var add = await setup.Owner.Client.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        var memberId = ParseGuid(add.Body, "value");
        var delete = await setup.Owner.Client.SendAsync(HttpMethod.Delete, $"/repository-member/{memberId}").ConfigureAwait(false);
        Assert.Equal(204, delete.StatusCode);
        await Baselines.CaptureApiAsync("owner-remove-member", delete).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OutsiderCannotAddMember()
    {
        BeginScenario();
        var setup = await SeedAsync("outsider-add-denied").ConfigureAwait(false);
        var add = await setup.Outsider.Client.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        Assert.Equal(403, add.StatusCode);
        await Baselines.CaptureApiAsync("outsider-add-denied", add).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task AnonymousCannotAddMember()
    {
        BeginScenario();
        var setup = await SeedAsync("anon-add-denied").ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var add = await anon.PostAsync("/repository-member", new
        {
            modelToCreate = new
            {
                repositoryId = new { value = setup.Repository.RepositoryId },
                userId = new { value = setup.Candidate.UserId },
                role = 1,
            },
        }).ConfigureAwait(false);
        Assert.Equal(401, add.StatusCode);
        await Baselines.CaptureApiAsync("anon-add-denied", add).ConfigureAwait(false);
    }

    private async Task<RepositoryMemberSmokeScenario> SeedAsync(string prefix)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"rm-smoke-owner-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"rm-smoke-reader-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"rm-smoke-outsider-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var candidate = await identity.RegisterUserAsync($"rm-smoke-candidate-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repositories.CreateAsync(owner, $"rm-smoke-{prefix}-{Context.RunSuffix}", "Repository Member Smoke", isPrivate: true)
            .ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);
        return new RepositoryMemberSmokeScenario(owner, reader, outsider, candidate, repository);
    }

    private static Guid ParseGuid(string body, string propertyName)
    {
        using var doc = JsonDocument.Parse(body);
        var value = doc.RootElement.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Object
            ? value.GetProperty("value").GetGuid()
            : value.GetGuid();
    }

    private sealed record RepositoryMemberSmokeScenario(
        AuthenticatedClient Owner,
        AuthenticatedClient Reader,
        AuthenticatedClient Outsider,
        AuthenticatedClient Candidate,
        RepositorySeed Repository);
}
