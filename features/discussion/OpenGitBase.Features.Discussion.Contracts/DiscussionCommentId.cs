using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Contracts;

public record DiscussionCommentId : Identifier<Guid, DiscussionCommentId>;
