using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Users.Contracts.Models;

public record UserId : Identifier<Guid, UserId>;
