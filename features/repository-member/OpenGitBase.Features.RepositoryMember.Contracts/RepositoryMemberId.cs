using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.RepositoryMember.Contracts;

public record RepositoryMemberId : Identifier<Guid, RepositoryMemberId>;
