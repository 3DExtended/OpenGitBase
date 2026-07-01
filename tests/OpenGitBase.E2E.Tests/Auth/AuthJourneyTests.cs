using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Auth;

[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class AuthJourneyTests : E2eTestBase
{
    [RequiresComposeFact]
    public async Task RegisterCapturedEmailVerifyAndLogin()
    {
        BeginScenario();
        var username = $"journey-{Context.RunSuffix}";
        var email = $"{username}@example.com";
        var password = "Password123!";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);

        Transcript.Describe("Register new user via public API");
        var register = await anon.PostAsync("/register/register", new { username, email, password }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201 or 204, $"Register failed with {register.StatusCode}: {register.Body}");

        Transcript.Describe("Fetch verification email from E2E capture endpoint");
        var emails = await anon.GetCapturedEmailsAsync(email).ConfigureAwait(false);
        Assert.NotEmpty(emails);
        var verificationCode = E2eScenarioHelpers.ParseVerificationCode(emails[0].HtmlBody);
        Context.Normalizer.RegisterToken("VERIFY_TOKEN", verificationCode);
        await Baselines.CaptureSideChannelAsync("verify-email", "emails", new
        {
            emails[0].Subject,
            Body = Context.Normalizer.Normalize(emails[0].HtmlBody),
        }).ConfigureAwait(false);

        Transcript.Describe("Verify email using captured code (no debug shortcut)");
        var verify = await anon.PostAsync("/account/verify-email", new
        {
            username,
            verificationToken = verificationCode,
        }).ConfigureAwait(false);
        Assert.Equal(200, verify.StatusCode);
        await Baselines.CaptureApiAsync("verify-email", verify).ConfigureAwait(false);

        Transcript.Describe("Login with verified credentials");
        var login = await anon.PostAsync("/signin/login", new { username, password }).ConfigureAwait(false);
        Assert.Equal(200, login.StatusCode);
        await Baselines.CaptureApiAsync("login", login).ConfigureAwait(false);
        await AssertBaselinesAsync().ConfigureAwait(false);
    }
}
