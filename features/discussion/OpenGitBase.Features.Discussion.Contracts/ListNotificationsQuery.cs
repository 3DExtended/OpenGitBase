using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ListNotificationsQuery : IQuery<IReadOnlyList<NotificationDto>, ListNotificationsQuery>
{
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
    public bool UnreadOnly { get; set; }
}
