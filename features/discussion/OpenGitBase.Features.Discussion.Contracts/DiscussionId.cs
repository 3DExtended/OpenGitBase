using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Contracts;

public record DiscussionId : Identifier<Guid, DiscussionId>;
