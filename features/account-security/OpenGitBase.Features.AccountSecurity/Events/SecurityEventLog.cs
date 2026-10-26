using Microsoft.Extensions.Logging;

namespace OpenGitBase.Features.AccountSecurity.Events;

/// <summary>
/// Writes security events in one shape: <c>security.two_factor.enabled user=… ip=… method=totp</c>, with the
/// event name, user id, remote IP and details also as structured values.
/// </summary>
internal static class SecurityEventLog
{
    private const string Template = "{SecurityEvent} user={UserId} ip={RemoteIp} {Detail}";

    public static void Write(
        ILogger logger,
        SecurityEvent securityEvent,
        Guid? userId,
        string? remoteIp,
        string detail = ""
    ) =>
        logger.Log(
            securityEvent.Level,
            securityEvent.EventId,
            Template,
            securityEvent.Name,
            (object?)userId ?? "unknown",
            remoteIp ?? "unknown",
            detail
        );
}
