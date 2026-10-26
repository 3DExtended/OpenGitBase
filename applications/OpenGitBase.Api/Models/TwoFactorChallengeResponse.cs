namespace OpenGitBase.Api.Models;

/// <summary>The sign-in answer for an account with 2FA on: no session yet, only a challenge to answer.</summary>
public class TwoFactorChallengeResponse
{
    public bool TwoFactorRequired { get; set; } = true;

    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>The second factors the account can answer with: <c>totp</c>, <c>passkey</c> and <c>recovery</c>.</summary>
    public IReadOnlyList<string> Methods { get; set; } = [];
}
