using Microsoft.Extensions.Logging;

namespace OpenGitBase.Features.AccountSecurity.Events;

/// <summary>
/// A security event as the log records it. The name follows the common <c>security.&lt;subject&gt;.&lt;what&gt;</c>
/// scheme; it is the event id's name and the first word of the message, so operators can grep for it.
/// </summary>
internal sealed record SecurityEvent(int Id, string Name, LogLevel Level)
{
    public static readonly SecurityEvent TwoFactorEnabled = new(7001, "security.two_factor.enabled", LogLevel.Information);

    public static readonly SecurityEvent TwoFactorDisabled = new(7002, "security.two_factor.disabled", LogLevel.Information);

    public static readonly SecurityEvent TwoFactorAdminReset = new(7003, "security.two_factor.admin_reset", LogLevel.Warning);

    public static readonly SecurityEvent TwoFactorSignedIn = new(7004, "security.two_factor.signed_in", LogLevel.Information);

    public static readonly SecurityEvent SecondFactorLockedOut = new(7010, "security.second_factor.locked_out", LogLevel.Warning);

    public static readonly SecurityEvent RecoveryCodeUsed = new(7020, "security.recovery_code.used", LogLevel.Warning);

    public static readonly SecurityEvent RecoveryCodesRegenerated = new(
        7021,
        "security.recovery_codes.regenerated",
        LogLevel.Information
    );

    public static readonly SecurityEvent PasskeyAdded = new(7030, "security.passkey.added", LogLevel.Information);

    public static readonly SecurityEvent PasskeyRemoved = new(7031, "security.passkey.removed", LogLevel.Information);

    public static readonly SecurityEvent PasskeySignedIn = new(7032, "security.passkey.signed_in", LogLevel.Information);

    public static readonly SecurityEvent PasskeyRejected = new(7033, "security.passkey.rejected", LogLevel.Information);

    public static readonly SecurityEvent PasskeyRegistrationRejected = new(
        7034,
        "security.passkey.registration_rejected",
        LogLevel.Information
    );

    public static readonly SecurityEvent NotificationFailed = new(7090, "security.notification.failed", LogLevel.Warning);

    public EventId EventId => new(Id, Name);
}
