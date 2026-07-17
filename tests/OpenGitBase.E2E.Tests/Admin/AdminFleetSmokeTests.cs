using OpenGitBase.E2E.Core;
using OpenGitBase.E2E.Core.Fixtures;

namespace OpenGitBase.E2E.Tests.Admin;

[Collection("Compose")]
[Trait("Category", "Admin")]
[Trait("Tag", "Smoke")]
[Trait("RequiresCompose", "true")]
[E2eTier(0)]
public class AdminFleetSmokeTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task PlatformAdminCanListStorageNodes()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Platform admin lists storage nodes");
        var response = await admin.Client.GetAsync("/admin/storage-nodes").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-list-storage-nodes", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task OutsiderDeniedOnStorageNodes()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var outsider = await identity.RegisterUserAsync($"admin-smoke-out-{Context.RunSuffix}").ConfigureAwait(false);
        Transcript.Describe("Outsider denied on admin storage nodes");
        var response = await outsider.Client.GetAsync("/admin/storage-nodes").ConfigureAwait(false);
        Assert.Equal(403, response.StatusCode);
        await Baselines.CaptureApiAsync("outsider-storage-nodes-denied", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task AnonymousDeniedOnStorageNodes()
    {
        BeginScenario();
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        Transcript.Describe("Anonymous denied on admin storage nodes");
        var response = await anon.GetAsync("/admin/storage-nodes").ConfigureAwait(false);
        Assert.Equal(401, response.StatusCode);
        await Baselines.CaptureApiAsync("anon-storage-nodes-denied", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PlatformAdminCanReadReplicationSummary()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Platform admin reads replication summary");
        var response = await admin.Client.GetAsync("/admin/storage-nodes/replication-summary").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-replication-summary", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PlatformAdminCanListRepositoriesReplication()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Platform admin lists repository replication");
        var response = await admin.Client.GetAsync("/admin/repositories").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-repositories", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PlatformAdminCanListStorageEnrollments()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Platform admin lists storage enrollments");
        var response = await admin.Client.GetAsync("/admin/storage-enrollments").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-enrollments", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PlatformAdminCanReadFleetDispatcherKey()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Platform admin reads fleet dispatcher SSH public key");
        var response = await admin.Client.GetAsync("/admin/fleet/dispatcher-ssh-public-key").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-fleet-key", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task WriterDeniedOnReplicationSummary()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var writer = await identity.RegisterUserAsync($"admin-smoke-writer-{Context.RunSuffix}").ConfigureAwait(false);
        Transcript.Describe("Writer denied on replication summary");
        var response = await writer.Client.GetAsync("/admin/storage-nodes/replication-summary").ConfigureAwait(false);
        Assert.Equal(403, response.StatusCode);
        await Baselines.CaptureApiAsync("writer-replication-denied", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task MissingRepositoryReplicationReturnsNotFoundForAdmin()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Admin gets 404 for unknown repository replication");
        var response = await admin.Client
            .GetAsync("/admin/repositories/00000000-0000-0000-0000-000000000000/replication")
            .ConfigureAwait(false);
        Assert.Equal(404, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-missing-repo-replication", response).ConfigureAwait(false);
    }

    [RequiresComposeFact]
    public async Task PlatformAdminCanListComputeNodes()
    {
        BeginScenario();
        var identity = new IdentityFixture(Context, Transcript);
        var admin = await identity.LoginPlatformAdminAsync().ConfigureAwait(false);
        Transcript.Describe("Platform admin lists compute nodes");
        var response = await admin.Client.GetAsync("/admin/compute-nodes").ConfigureAwait(false);
        Assert.Equal(200, response.StatusCode);
        await Baselines.CaptureApiAsync("admin-compute-nodes", response).ConfigureAwait(false);
    }
}
