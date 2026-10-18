namespace OpenGitBase.Api.Models;

public class TotpSetupResponse
{
    /// <summary>The <c>otpauth://</c> URI the web app renders as a QR code.</summary>
    public string OtpauthUri { get; set; } = string.Empty;

    /// <summary>The same secret as Base32, for typing it into the app by hand.</summary>
    public string Secret { get; set; } = string.Empty;
}
