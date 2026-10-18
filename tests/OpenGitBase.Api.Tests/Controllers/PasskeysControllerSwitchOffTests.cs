using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Tests.Base;

namespace OpenGitBase.Api.Tests.Controllers;

/// <summary>An empty <c>WebAuthn:RelyingPartyId</c>: every passkey endpoint is missing and the UI hides passkeys.</summary>
public class PasskeysControllerSwitchOffTests : TwoFactorControllerTestBase
{
    public PasskeysControllerSwitchOffTests(WebApplicationFactory<ApiEntryPoint> factory)
        : base(factory, passkeysEnabled: false) { }

    [Fact]
    public async Task EveryPasskeyEndpoint_IsNotFound()
    {
        using var browser = NewBrowser();
        await RegisterAsync(browser, "nopasskeys");
        (await ReauthenticateAsync(browser)).EnsureSuccessStatusCode();
        var someId = Guid.NewGuid();

        HttpResponseMessage[] responses =
        [
            await browser.GetAsync("/account/passkeys"),
            await browser.PostAsync("/account/passkeys/registration-options", null),
            await browser.PostAsJsonAsync("/account/passkeys", new { name = "Laptop", attestationResponse = new { } }),
            await browser.PatchAsJsonAsync($"/account/passkeys/{someId}", new RenamePasskeyDto { Name = "Phone" }),
            await browser.DeleteAsync($"/account/passkeys/{someId}"),
            await browser.PostAsync("/account/reauthenticate/passkey-options", null),
            await browser.PostAsJsonAsync("/account/reauthenticate", new { passkeyAssertion = new { } }),
            await browser.PostAsync("/signin/passkey/options", null),
            await browser.PostAsJsonAsync("/signin/passkey", new { assertion = new { } }),
        ];

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode));
        var status = await browser.GetFromJsonAsync<AccountSecurityResponse>("/account/security");
        Assert.False(status?.PasskeysAvailable);
    }
}
