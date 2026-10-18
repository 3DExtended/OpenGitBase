using Microsoft.Extensions.Logging;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.SecondFactor;

/// <summary>
/// The per-account brake on guessing second-factor codes. Failures count across challenges, so
/// someone who knows the password cannot reset the count by signing in again.
/// </summary>
internal static class SecondFactorAttempts
{
    public const int MaxConsecutiveFailures = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static bool IsLockedOut(UserCredentialsEntity credentials, DateTimeOffset now) =>
        credentials.SecondFactorLockedUntil is { } lockedUntil && lockedUntil > now;

    /// <summary>Counts a failure; returns true when it started a lockout.</summary>
    public static bool RecordFailure(UserCredentialsEntity credentials, DateTimeOffset now)
    {
        credentials.SecondFactorFailedAttempts++;
        if (credentials.SecondFactorFailedAttempts < MaxConsecutiveFailures)
        {
            return false;
        }

        credentials.SecondFactorFailedAttempts = 0;
        credentials.SecondFactorLockedUntil = now + LockoutDuration;
        return true;
    }

    public static void LogLockout(ILogger logger, UserCredentialsEntity credentials, string step) =>
        logger.LogWarning(
            "Second-factor lockout for user {UserId} after {Failures} wrong codes during {Step}; locked until {LockedUntil}",
            credentials.UserId,
            MaxConsecutiveFailures,
            step,
            credentials.SecondFactorLockedUntil
        );

    public static void RecordSuccess(UserCredentialsEntity credentials)
    {
        credentials.SecondFactorFailedAttempts = 0;
        credentials.SecondFactorLockedUntil = null;
    }
}
