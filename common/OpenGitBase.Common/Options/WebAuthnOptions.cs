namespace OpenGitBase.Common.Options;

/// <summary>The WebAuthn relying party behind passkeys (<c>WebAuthn:*</c>).</summary>
public sealed class WebAuthnOptions
{
    /// <summary>
    /// The registrable domain passkeys are bound to, such as <c>opengitbase.com</c>. Empty turns passkeys
    /// off: their endpoints answer 404 and the web app hides them.
    /// </summary>
    public string RelyingPartyId { get; set; } = string.Empty;

    /// <summary>The name the browser shows when it asks to create a passkey.</summary>
    public string RelyingPartyName { get; set; } = "OpenGitBase";

    /// <summary>The exact origins the browser reports, scheme and port included.</summary>
    public string[] Origins { get; set; } = [];

    public bool Enabled => !string.IsNullOrWhiteSpace(RelyingPartyId);
}
