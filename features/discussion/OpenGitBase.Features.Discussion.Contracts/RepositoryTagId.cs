using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Contracts;

public record RepositoryTagId : Identifier<Guid, RepositoryTagId>;
