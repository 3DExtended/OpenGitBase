using System.Text.Json;

namespace OpenGitBase.Api.Models;

/// <summary>
/// The second sign-in step: exactly one of <see cref="Code"/>, <see cref="RecoveryCode"/> and
/// <see cref="PasskeyAssertion"/>.
/// </summary>
public class TwoFactorSignInDto
{
    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>The authenticator code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>One of the account's recovery codes, with or without the dash, in any case.</summary>
    public string? RecoveryCode { get; set; }

    /// <summary>
    /// An assertion for <c>POST /signin/two-factor/passkey-options</c>, as <c>PublicKeyCredential.toJSON()</c>
    /// returns it.
    /// </summary>
    public JsonElement? PasskeyAssertion { get; set; }
}
