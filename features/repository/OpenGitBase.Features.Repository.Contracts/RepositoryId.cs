using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Repository.Contracts;

public record RepositoryId : Identifier<Guid, RepositoryId>;
