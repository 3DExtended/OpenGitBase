using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Repository;

[Collection("Compose")]
[Trait("Category", "Repository")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(5)]
public class RepositorySettingsSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task OwnerCanGetRepositoryById()
    {
        BeginScenario();
        var setup = await SeedAsync("get-id").ConfigureAwait(false);
        var response = await setup.Owner.Client.GetAsync($"/repository/{setup.Repository.RepositoryId}").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("get-repository", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanReadUsage()
    {
        BeginScenario();
        var setup = await SeedAsync("usage").ConfigureAwait(false);
        var response = await setup.Owner.Client.GetAsync($"/repository/{setup.Repository.RepositoryId}/usage").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("get-usage", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanUpdateRepositoryMetadata()
    {
        BeginScenario();
        var setup = await SeedAsync("update").ConfigureAwait(false);
        var response = await setup.Owner.Client.SendAsync(HttpMethod.Put, $"/repository/{setup.Repository.RepositoryId}", new
        {
            name = "Repository Settings Smoke Updated",
            isPrivate = true,
        }).ConfigureAwait(false);
        Assert.Equal(204, response.StatusCode);
        await Baselines.CaptureApiAsync("update-metadata", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReaderCannotUpdateRepositoryMetadata()
    {
        BeginScenario();
        var setup = await SeedAsync("reader-update").ConfigureAwait(false);
        var response = await setup.Reader.Client.SendAsync(HttpMethod.Put, $"/repository/{setup.Repository.RepositoryId}", new
        {
            name = "Reader denied",
            isPrivate = true,
        }).ConfigureAwait(false);
        Assert.Equal(403, response.StatusCode);
        await Baselines.CaptureApiAsync("reader-update-denied", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanReadDefaultBranchSetting()
    {
        BeginScenario();
        var setup = await SeedAsync("default-branch-get").ConfigureAwait(false);
        var response = await setup.Owner.Client.GetAsync($"/repository/{setup.Repository.RepositoryId}/settings/default-branch").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("default-branch-get", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanPatchDefaultBranchSetting()
    {
        BeginScenario();
        var setup = await SeedAsync("default-branch-patch").ConfigureAwait(false);
        var response = await setup.Owner.Client.SendAsync(HttpMethod.Patch, $"/repository/{setup.Repository.RepositoryId}/settings/default-branch", new
        {
            defaultBranchName = "main",
        }).ConfigureAwait(false);
        Assert.True(response.StatusCode is 200 or 400);
        await Baselines.CaptureApiAsync("default-branch-patch", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanListProtectedBranchRules()
    {
        BeginScenario();
        var setup = await SeedAsync("list-rules").ConfigureAwait(false);
        var response = await setup.Owner.Client.GetAsync($"/repository/{setup.Repository.RepositoryId}/protected-branch-rules").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("list-rules", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OwnerCanCreateProtectedBranchRule()
    {
        BeginScenario();
        var setup = await SeedAsync("create-rule").ConfigureAwait(false);
        var response = await setup.Owner.Client.PostAsync($"/repository/{setup.Repository.RepositoryId}/protected-branch-rules", new
        {
            pattern = "main",
            blockDirectPush = true,
            allowedPushRoles = 2,
            requiredApprovalCount = 0,
            mergeRoleThreshold = 2,
            forcePushPolicy = 0,
            pushRules = Array.Empty<object>(),
        }).ConfigureAwait(false);
        Assert.Equal(201, response.StatusCode);
        await Baselines.CaptureApiAsync("create-rule", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task ReaderCannotListProtectedBranchRules()
    {
        BeginScenario();
        var setup = await SeedAsync("reader-rules-denied").ConfigureAwait(false);
        var response = await setup.Reader.Client.GetAsync($"/repository/{setup.Repository.RepositoryId}/protected-branch-rules").ConfigureAwait(false);
        Assert.Equal(403, response.StatusCode);
        await Baselines.CaptureApiAsync("reader-list-rules-denied", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task AnonymousCannotReadProtectedBranchRules()
    {
        BeginScenario();
        var setup = await SeedAsync("anon-rules-denied").ConfigureAwait(false);
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var response = await anon.GetAsync($"/repository/{setup.Repository.RepositoryId}/protected-branch-rules").ConfigureAwait(false);
        Assert.Equal(401, response.StatusCode);
        await Baselines.CaptureApiAsync("anon-list-rules-denied", response).ConfigureAwait(false);
    }

    private async Task<RepositorySettingsSmokeScenario> SeedAsync(string prefix)
    {
        var identity = new IdentityFixture(Context, Transcript);
        var repositories = new RepositoryFixture(Transcript, Context.Normalizer);
        var owner = await identity.RegisterUserAsync($"repo-settings-smoke-owner-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var reader = await identity.RegisterUserAsync($"repo-settings-smoke-reader-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var outsider = await identity.RegisterUserAsync($"repo-settings-smoke-outsider-{prefix}-{Context.RunSuffix}").ConfigureAwait(false);
        var repository = await repositories.CreateAsync(
            owner,
            $"repo-settings-smoke-{prefix}-{Context.RunSuffix}",
            "Repository Settings Smoke",
            isPrivate: true).ConfigureAwait(false);
        await repositories.AddMemberAsync(owner, repository.RepositoryId, reader.UserId, role: 1).ConfigureAwait(false);
        return new RepositorySettingsSmokeScenario(owner, reader, outsider, repository);
    }

    private sealed record RepositorySettingsSmokeScenario(
        AuthenticatedClient Owner,
        AuthenticatedClient Reader,
        AuthenticatedClient Outsider,
        RepositorySeed Repository);
}
