using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Contracts;

public record NotificationId : Identifier<Guid, NotificationId>;
