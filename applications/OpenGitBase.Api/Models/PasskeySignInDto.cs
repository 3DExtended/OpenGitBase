using System.Text.Json;

namespace OpenGitBase.Api.Models;

public class PasskeySignInDto
{
    /// <summary>
    /// An assertion for <c>POST /signin/passkey/options</c>, as <c>PublicKeyCredential.toJSON()</c> returns
    /// it. The challenge it signed identifies the sign-in, so no separate token is needed.
    /// </summary>
    public JsonElement Assertion { get; set; }
}
