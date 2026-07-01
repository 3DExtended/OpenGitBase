using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Auth;

[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class IdentitySeedTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task SeedCoreRolesCreatesUsers()
    {
        BeginScenario();
        var fixture = new IdentityFixture(Context, Transcript);
        await fixture.SeedCoreRolesAsync().ConfigureAwait(false);
        Transcript.Describe("Verify seeded admin can access account endpoint");
        var me = await fixture.AsAdmin.Client.GetAsync("/account/me").ConfigureAwait(false);
        await Baselines.CaptureApiAsync("admin-me", me).ConfigureAwait(false);
        Assert.Equal(200, me.StatusCode);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }
}
