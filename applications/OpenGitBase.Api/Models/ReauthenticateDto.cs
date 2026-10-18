using System.Text.Json;

namespace OpenGitBase.Api.Models;

public class ReauthenticateDto
{
    /// <summary>Empty for accounts without a password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>The authenticator code; this or <see cref="RecoveryCode"/> is required while 2FA is on.</summary>
    public string? Code { get; set; }

    /// <summary>A recovery code instead of the authenticator code.</summary>
    public string? RecoveryCode { get; set; }

    /// <summary>
    /// An assertion for <c>POST /account/reauthenticate/passkey-options</c>, as <c>PublicKeyCredential.toJSON()</c>
    /// returns it. Replaces password and code.
    /// </summary>
    public JsonElement? PasskeyAssertion { get; set; }
}
