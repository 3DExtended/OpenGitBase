using System.Text.Json;
using OpenGitBase.Common.Tests.Testing;
using OpenGitBase.E2E.Core;

namespace OpenGitBase.E2E.Tests.Security;

/// <summary>
/// mfa-06 on the real stack: security emails for turning 2FA on and for a lockout, then the platform admin
/// resets the locked-out user, who signs in with the password alone.
/// </summary>
[Collection("Compose")]
[Trait("Category", "Auth")]
[Trait("RequiresCompose", "true")]
[E2eTier(1)]
public class TwoFactorAdminResetE2eTests : E2eTestBase
{
    private const string Password = "Password123!";

    [RequiresComposeFact]
    public async Task LockedOutUser_AdminReset_PasswordSignsInAgain()
    {
        BeginScenario();
        var username = $"mfa-reset-{Context.RunSuffix}";
        var address = $"{username}@example.com";
        var anon = new E2eApiClient(Transcript, Context.Normalizer);
        var register = await anon.PostAsync("/register/register", new
        {
            username,
            email = address,
            password = Password,
        }).ConfigureAwait(false);
        Assert.True(register.StatusCode is 200 or 201, register.Body);
        using var session = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(register));

        Transcript.Describe("Turning 2FA on emails the account");
        Assert.Equal(204, (await session.PostAsync("/account/reauthenticate", new { password = Password }).ConfigureAwait(false)).StatusCode);
        var setup = await session.PostAsync("/account/two-factor/totp/setup").ConfigureAwait(false);
        Assert.Equal(200, setup.StatusCode);
        string secret;
        using (var setupJson = JsonDocument.Parse(setup.Body))
        {
            secret = setupJson.RootElement.GetProperty("secret").GetString()!;
        }

        var step = TotpCodes.StepAt(DateTimeOffset.UtcNow);
        var confirm = await session.PostAsync("/account/two-factor/totp/confirm", new { code = TotpCodes.ForStep(secret, step) })
            .ConfigureAwait(false);
        Assert.Equal(200, confirm.StatusCode);
        var turnedOn = Assert.Single(await EmailsAsync(anon, address, "Two-factor authentication turned on").ConfigureAwait(false));
        Assert.Contains("/settings/security", turnedOn.HtmlBody, StringComparison.Ordinal);

        Transcript.Describe("Five wrong second steps lock the second step and email the account");
        var wrong = WrongCode(secret, step);
        var lastStatus = 0;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var login = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
            using var challenge = JsonDocument.Parse(login.Body);
            var answer = await anon.PostAsync("/signin/two-factor", new
            {
                challengeToken = challenge.RootElement.GetProperty("challengeToken").GetString(),
                code = wrong,
            }).ConfigureAwait(false);
            lastStatus = answer.StatusCode;
        }

        Assert.Equal(429, lastStatus);
        var lockout = Assert.Single(await EmailsAsync(anon, address, "Sign-in blocked after failed second steps").ConfigureAwait(false));
        Assert.Contains("password was entered correctly", lockout.HtmlBody, StringComparison.Ordinal);

        Transcript.Describe("The platform admin resets the user's second factors");
        var admin = await new IdentityFixture(Context, Transcript).LoginPlatformAdminAsync().ConfigureAwait(false);
        var reset = await admin.Client.PostAsync($"/admin/users/{username}/two-factor/reset").ConfigureAwait(false);
        Assert.Equal(204, reset.StatusCode);
        Assert.Single(await EmailsAsync(anon, address, "Two-factor authentication reset by an administrator").ConfigureAwait(false));

        Transcript.Describe("The password alone signs in again; the user cannot reset others");
        var passwordOnly = await anon.PostAsync("/signin/login", new { username, password = Password }).ConfigureAwait(false);
        Assert.Equal(200, passwordOnly.StatusCode);
        Assert.DoesNotContain("twoFactorRequired", passwordOnly.Body, StringComparison.Ordinal);
        using var signedIn = new E2eApiClient(Transcript, Context.Normalizer, E2eScenarioHelpers.ParseJwtToken(passwordOnly));
        var me = await signedIn.GetAsync("/account/me").ConfigureAwait(false);
        Assert.Equal(200, me.StatusCode);
        Assert.Contains("\"twoFactorEnabled\":false", me.Body, StringComparison.Ordinal);
        Assert.Equal(403, (await signedIn.PostAsync($"/admin/users/{admin.Username}/two-factor/reset").ConfigureAwait(false)).StatusCode);
    }

    private static async Task<IReadOnlyList<CapturedEmail>> EmailsAsync(E2eApiClient client, string to, string subject) =>
        (await client.GetCapturedEmailsAsync(to).ConfigureAwait(false)).Where(email => email.Subject == subject).ToList();

    /// <summary>A six-digit code that matches none of the steps the API accepts around <paramref name="step"/>.</summary>
    private static string WrongCode(string secret, long step)
    {
        var valid = Enumerable.Range(-1, 4).Select(offset => TotpCodes.ForStep(secret, step + offset)).ToHashSet();
        return new[] { "000000", "111111", "222222", "333333", "444444" }.First(code => !valid.Contains(code));
    }
}
