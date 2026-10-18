namespace OpenGitBase.Api.Models;

/// <summary>Why a second-factor step failed, as a stable code the clients translate.</summary>
public class SecondFactorErrorResponse
{
    public const string InvalidCode = "invalid_code";
    public const string ChallengeExpired = "challenge_expired";
    public const string LockedOut = "locked_out";
    public const string InvalidCredentials = "invalid_credentials";
    public const string SecondFactorRequired = "second_factor_required";
    public const string ReauthenticationUnavailable = "reauthentication_unavailable";
    public const string ReauthenticationRequired = "reauthentication_required";
    public const string NoPendingSetup = "no_pending_setup";
    public const string TwoFactorNotEnabled = "two_factor_not_enabled";

    public string Error { get; set; } = string.Empty;

    /// <summary>Only set for <see cref="LockedOut"/>.</summary>
    public DateTimeOffset? LockedUntil { get; set; }
}
