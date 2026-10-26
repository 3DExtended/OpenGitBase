using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

/// <summary>
/// Emails the account's address about a security event, with its time, the remote IP and a link to the
/// security settings. A send failure is logged and never reported back, so the action that caused the
/// event stands.
/// </summary>
public class SendSecurityNotificationQuery : IQuery<Unit, SendSecurityNotificationQuery>
{
    public UserId UserId { get; set; } = null!;

    public SecurityNotification Notification { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The address the event came from; null leaves it out of the email.</summary>
    public string? RemoteIpAddress { get; set; }
}
