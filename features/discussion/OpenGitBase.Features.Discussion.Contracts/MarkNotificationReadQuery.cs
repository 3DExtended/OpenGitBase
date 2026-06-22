using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class MarkNotificationReadQuery : IQuery<Unit, MarkNotificationReadQuery>
{
    public NotificationId NotificationId { get; set; } = NotificationId.From(Guid.Empty);
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
}
