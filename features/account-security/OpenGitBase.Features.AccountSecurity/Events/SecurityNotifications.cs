using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.AccountSecurity.Events;

internal static class SecurityNotifications
{
    /// <summary>Emails the user about the event; failures are logged by the notification and never thrown.</summary>
    public static Task NotifyAsync(
        this IQueryProcessor queryProcessor,
        Guid userId,
        SecurityNotification notification,
        DateTimeOffset occurredAt,
        string? remoteIp,
        CancellationToken cancellationToken
    ) =>
        queryProcessor.RunQueryAsync(
            new SendSecurityNotificationQuery
            {
                UserId = UserId.From(userId),
                Notification = notification,
                OccurredAt = occurredAt,
                RemoteIpAddress = remoteIp,
            },
            cancellationToken
        );
}
